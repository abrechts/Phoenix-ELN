Imports System.Data
Imports System.Linq.Expressions
Imports System.Xml.Linq
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore

''' <summary>
''' Owns everything related to full-text search: the in-memory SearchIndex FTS5 virtual table (schema,
''' hydration, querying), its persisted on-disk mirror tblSearchIndex (the sync-staging table server sync
''' pushes up from), and keeping both in sync with the protocol item satellite tables (reagents, products,
''' solvents, auxiliaries, reference reactants, separators, embedded files and comments).
''' </summary>
'''
Public Class FullTextSearch

    ''' <summary>
    ''' The dedicated, long-lived connection backing the in-memory full-text SearchIndex: a second connection to
    ''' the same on-disk SQLite file DBContext uses (SQLite supports multiple concurrent readers), with a
    ''' ':memory:' database ATTACHed as "mem" holding the actual FTS5 table. Kept open for the whole app session -
    ''' closing it would discard the in-memory database along with everything indexed into it. Set once by
    ''' <see cref="InitializeMemoryIndexConnection"/> at startup, before any search query or SaveChanges call
    ''' touches the SearchIndex.
    ''' </summary>
    '''
    Public Shared Property MemoryIndexConnection As SqliteConnection


    ''' <summary>
    ''' DDL for the full-text SearchIndex FTS5 virtual table, created inside the "mem" schema ATTACHed by
    ''' <see cref="InitializeMemoryIndexConnection"/> - it never exists on disk.
    ''' </summary>
    '''
    Friend Shared ReadOnly SearchIndexTableDDL As String =
        "CREATE VIRTUAL TABLE IF NOT EXISTS mem.SearchIndex USING fts5(ProtocolItemID UNINDEXED, ExperimentID UNINDEXED, Content, " +
        "tokenize=""unicode61 remove_diacritics 2"");"


    ''' <summary>
    ''' DDL for the persisted on-disk tblSearchIndex table (just the CREATE TABLE - kept as a single statement,
    ''' since ExecuteSqlRaw's multi-statement support isn't guaranteed the way raw ADO.NET SqliteCommand's is).
    ''' Shared between ElnDbContext's constructor self-heal check (<see cref="EnsureTblSearchIndexExists"/>) and
    ''' DbUpgradeLocal's documented, versioned schema history, so the two can't drift apart.
    ''' </summary>
    '''
    Friend Shared ReadOnly TblSearchIndexTableDDL As String =
        "CREATE TABLE IF NOT EXISTS tblSearchIndex (
        ProtocolItemID VARCHAR(36) PRIMARY KEY NOT NULL REFERENCES tblProtocolItems(GUID) ON DELETE CASCADE,
        ExperimentID VARCHAR(25) NOT NULL,
        Content TEXT NOT NULL,
        SyncState TINYINT DEFAULT 0);"


    ''' <summary>
    ''' DDL for tblSearchIndex's ExperimentID index. Shared for the same reason as <see cref="TblSearchIndexTableDDL"/>.
    ''' </summary>
    '''
    Friend Shared ReadOnly TblSearchIndexIndexDDL As String =
        "CREATE INDEX IF NOT EXISTS idx_tblSearchIndex_ExperimentID ON tblSearchIndex(ExperimentID);"


    ''' <summary>
    ''' Ensures the on-disk tblSearchIndex table exists. Called from ElnDbContext's constructor so full-text
    ''' search works regardless of whether DbUpgradeLocal.Upgrade has already run against this particular
    ''' database file - its invocation is gated by the app's own version-change detection, which doesn't fire
    ''' on every run (e.g. no version bump since last launch) or reliably for every database this context could
    ''' end up wrapping (e.g. one just restored from the server, or carried over from an older/foreign install).
    ''' </summary>
    '''
    Friend Shared Sub EnsureTblSearchIndexExists(searchContext As ElnDbContext)

        searchContext.Database.ExecuteSqlRaw(TblSearchIndexTableDDL)
        searchContext.Database.ExecuteSqlRaw(TblSearchIndexIndexDDL)

    End Sub


    ''' <summary>
    ''' Opens <see cref="MemoryIndexConnection"/>: a second connection to the same on-disk SQLite file DBContext
    ''' uses, with a ':memory:' database ATTACHed as "mem" holding the FTS5 SearchIndex table (freshly created
    ''' and empty - callers still need to populate it via <see cref="HydrateMemoryIndex"/>). Must be called once
    ''' at startup, before any search query or SaveChanges call touches the SearchIndex.
    ''' </summary>
    ''' <param name="sqliteFilePath">Path to the same local SQLite database file DBContext is opened against.</param>
    '''
    Public Shared Sub InitializeMemoryIndexConnection(sqliteFilePath As String)

        MemoryIndexConnection = New SqliteConnection("Data Source=" + sqliteFilePath)
        MemoryIndexConnection.Open()

        Using command = MemoryIndexConnection.CreateCommand()
            command.CommandText = "ATTACH DATABASE ':memory:' AS mem;"
            command.ExecuteNonQuery()
        End Using

        Using command = MemoryIndexConnection.CreateCommand()
            command.CommandText = SearchIndexTableDDL
            command.ExecuteNonQuery()
        End Using

    End Sub


    ''' <summary>
    ''' Refills the in-memory FTS5 SearchIndex table from the persisted tblSearchIndex table via a single
    ''' set-based INSERT...SELECT, executed entirely inside SQLite - no per-row .NET round-trips and no content
    ''' recalculation, unlike <see cref="RebuildSearchIndex"/>. Called once at every app startup (the in-memory
    ''' table always starts out empty each session, on top of whatever <see cref="InitializeMemoryIndexConnection"/>
    ''' just created), after any one-time backfill of tblSearchIndex itself (see <see cref="RebuildSearchIndex"/>)
    ''' has already completed. tblSearchIndex is read unqualified, resolving to "main" - the on-disk file that is
    ''' this same connection's default schema (see InitializeMemoryIndexConnection).
    ''' </summary>
    '''
    Public Shared Sub HydrateMemoryIndex()

        Using command = MemoryIndexConnection.CreateCommand()
            command.CommandText =
                "DELETE FROM mem.SearchIndex;
                INSERT INTO mem.SearchIndex(ProtocolItemID, ExperimentID, Content)
                SELECT ProtocolItemID, ExperimentID, Content FROM tblSearchIndex;"
            command.ExecuteNonQuery()
        End Using

    End Sub


    ''' <summary>
    ''' Gets if the persisted tblSearchIndex table currently contains no entries, e.g. because it was just
    ''' created by a schema upgrade and still needs its initial backfill via <see cref="RebuildSearchIndex"/>.
    ''' </summary>
    '''
    Public Shared Function IsSearchIndexEmpty(searchContext As ElnDbContext) As Boolean

        If Not searchContext.Database.IsSqlite() Then
            Throw New NotSupportedException("The full-text SearchIndex only exists on the local SQLite database.")
        End If

        Return Not searchContext.tblSearchIndex.AsNoTracking().Any()

    End Function


    ''' <summary>
    ''' A single searchable protocol item satellite row: its owning ProtocolItemID and computed text content.
    ''' </summary>
    '''
    Private Class SearchableRow

        Public Property ProtocolItemID As String
        Public Property Content As String

    End Class


    ''' <summary>
    ''' Per-table definitions of what's searchable, expressed as EF-translatable expression trees rather than
    ''' plain functions, so each is a single source of truth used two different ways: RebuildSearchIndex passes
    ''' one directly into a LINQ .Select(...), letting EF Core translate it into a SQL projection that only ever
    ''' fetches the referenced columns - crucially, never an unreferenced BLOB column like
    ''' tblEmbeddedFiles.FileBytes/IconImage, and the same protection automatically applies to any future table
    ''' with a BLOB column, with no separate special-casing needed. CollectSearchIndexOps (the incremental,
    ''' already-in-memory path) instead compiles the same expression into a delegate via SearchableEntityProjections
    ''' below. tblReagents, tblSolvents, tblAuxiliaries, tblProducts, tblRefReactants and tblComments are the
    ''' deliberate exceptions (see RebuildSearchIndex/GetSearchableRow/Build*Content) - the first five's content
    ''' combines several columns via ELNCalculations scaling logic that isn't SQL-translatable, and tblComments'
    ''' plain text needs to be extracted from its FlowDocument XAML via WPF's XamlReader, which can only run
    ''' client-side, never as part of a translated SQL query.
    ''' </summary>
    '''
    Private Shared ReadOnly SeparatorProjection As Expression(Of Func(Of tblSeparators, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Title}

    Private Shared ReadOnly EmbeddedFileProjection As Expression(Of Func(Of tblEmbeddedFiles, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.FileName + " " + item.FileComment}

    ' tblReagents, tblSolvents, tblAuxiliaries, tblProducts, tblRefReactants and tblComments are treated separately,
    ' see BuildReagentContent / BuildSolventContent / BuildAuxiliaryContent / BuildProductContent /
    ' BuildRefReactantContent / ExtractPlainText


    ''' <summary>
    ''' Compiled, type-erased dispatch table derived from the projections above (never declared separately),
    ''' keyed by entity CLR type. Used by the incremental (already-in-memory) path to find and invoke the right
    ''' projection for a given tracked entity. Built once, lazily, on first use.
    ''' </summary>
    '''
    Private Shared ReadOnly SearchableEntityProjections As New Lazy(Of Dictionary(Of Type, Func(Of Object, SearchableRow)))(
        Function()

            Dim dispatch As New Dictionary(Of Type, Func(Of Object, SearchableRow))

            AddProjection(dispatch, SeparatorProjection)
            AddProjection(dispatch, EmbeddedFileProjection)

            Return dispatch

        End Function)


    Private Shared Sub AddProjection(Of TEntity As Class)(dispatch As Dictionary(Of Type, Func(Of Object, SearchableRow)),
        projection As Expression(Of Func(Of TEntity, SearchableRow)))

        Dim compiled = projection.Compile()
        dispatch(GetType(TEntity)) = Function(entity) compiled(DirectCast(entity, TEntity))

    End Sub


    ''' <summary>
    ''' Builds a reagent's searchable content, e.g. "12.5 mg Triethylamine (2.50 M; 1.25 equiv; Merck 1234; 250 mmol;
    ''' 98.5%; 120 mmol/g resin)" - the weighed amount/unit, name, and every other parameter
    ''' CustomControls/Protocol Elements/Materials/ReagentContent.xaml displays (molarity, equivalents, source,
    ''' mmols, purity, resin load), each included only when actually present. Always uses this one canonical
    ''' form regardless of the experiment's IsDesignView state (which toggles which of the weight/equivalents
    ''' pair is primary on screen) - search content isn't view-mode-dependent. Unlike the XAML display, empty
    ''' parameters are omitted entirely rather than left as blank separators, since a search index has no use
    ''' for placeholder tokens.
    ''' </summary>
    '''
    Private Shared Function BuildReagentContent(name As String, source As String, grams As Double, density As Double?,
        isDisplayAsVolume As Boolean, isMolarity As Boolean, equivalents As Double, molarity As Double?, mMols As Double,
        purity As Double?, resinLoad As Double?) As String

        Dim content As New Text.StringBuilder()
        AppendAmountUnitPrefix(content, GetReagentWeightScale(grams, density, isDisplayAsVolume, isMolarity))
        content.Append(name)

        Dim detailParts = BuildMaterialDetailParts(equivalents, source, mMols, purity, resinLoad)
        If isMolarity AndAlso molarity.HasValue Then
            detailParts.Insert(0, ELNCalculations.SignificantDigitsString(molarity.Value, 3) + " M")
        End If

        AppendDetailParts(content, detailParts)
        Return content.ToString()

    End Function


    ''' <summary>
    ''' Gets the fitting weight/volume amount+unit for a reagent, mirroring WeightUnitConverter.Convert's core
    ''' logic (CustomControls/Converters/AmountUnitConverters.vb) minus its WPF-binding-specific plumbing. For a
    ''' molar reagent solution (IsMolarity), Grams actually holds the milliliters of solution, not a weight.
    ''' </summary>
    '''
    Private Shared Function GetReagentWeightScale(grams As Double, density As Double?, isDisplayAsVolume As Boolean,
        isMolarity As Boolean) As ELNCalculations.ScaleResult

        Dim calcAsWeight = Not (isDisplayAsVolume Xor isMolarity)
        If Not density.HasValue AndAlso Not (calcAsWeight Xor isMolarity) Then
            Return Nothing
        End If

        If calcAsWeight Then
            Dim effectiveGrams = If(isMolarity, grams * density.Value, grams)
            Return ELNCalculations.ScaleWeight(effectiveGrams)
        Else
            Dim milliliters = If(Not isMolarity, grams / density.Value, grams)
            Return ELNCalculations.ScaleVolume(milliliters)
        End If

    End Function


    ''' <summary>
    ''' Builds the (equivalents-unit; source; mmols; purity; resin load) detail parts shared by reagents and
    ''' reference reactants - the two satellite tables whose material makeup is otherwise identical (weight/volume,
    ''' equivalents, mmols, purity, resin load), differing only in whether a molar-solution concept applies
    ''' (reagents) or not (reference reactants). Each part is included only when actually present. Uses the long
    ''' "equiv"/"mEquiv" unit form (isShortUnit:=False), matching what ReagentContent.xaml/RefReactantContent.xaml
    ''' actually display in their default (non-design, i.e. "non-equivalents") view - the design view's "eq"/"mq"
    ''' shorthand is specific to that view's primary equivalents column, not used elsewhere.
    ''' </summary>
    '''
    Private Shared Function BuildMaterialDetailParts(equivalents As Double, source As String, mMols As Double,
        purity As Double?, resinLoad As Double?) As List(Of String)

        Dim detailParts As New List(Of String)

        Dim equivScale = ELNCalculations.ScaleEquivalent(equivalents, isShortUnit:=False)
        If equivScale IsNot Nothing Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(equivScale.Amount, 3) + " " + equivScale.Unit)
        End If

        If Not String.IsNullOrEmpty(source) Then
            detailParts.Add(source)
        End If

        Dim mMolScale = ELNCalculations.ScaleMMol(mMols)
        If mMolScale IsNot Nothing Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(mMolScale.Amount, 3) + " " + mMolScale.Unit)
        End If

        If purity.HasValue Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(purity.Value, 3) + "%")
        End If

        If resinLoad.HasValue Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(resinLoad.Value, 3) + " mmol/g resin")
        End If

        Return detailParts

    End Function


    ''' <summary>
    ''' Appends "{amount} {unit} " to content if scale is available (i.e. the underlying quantity is actually
    ''' populated - ScaleWeight/ScaleVolume/etc. return Nothing for an unset/zero "just added" row), or nothing
    ''' otherwise. Shared by every material content builder's leading amount+unit prefix.
    ''' </summary>
    '''
    Private Shared Sub AppendAmountUnitPrefix(content As Text.StringBuilder, scale As ELNCalculations.ScaleResult)

        If scale IsNot Nothing Then
            content.Append(ELNCalculations.SignificantDigitsString(scale.Amount, 3)).Append(" "c).
                Append(scale.Unit).Append(" "c)
        End If

    End Sub


    ''' <summary>
    ''' Appends " (part1; part2; ...)" to content if any detail parts were collected, or nothing otherwise. Shared
    ''' by every material content builder's trailing parenthetical detail list.
    ''' </summary>
    '''
    Private Shared Sub AppendDetailParts(content As Text.StringBuilder, detailParts As List(Of String))

        If detailParts.Count > 0 Then
            content.Append(" (").Append(String.Join("; ", detailParts)).Append(")"c)
        End If

    End Sub


    ''' <summary>
    ''' Builds a solvent's searchable content, e.g. "3.84 mL Methanol (1.52 volEquiv; Merck 12345)" - the
    ''' volume/weight amount+unit, name, and (equivalents-unit; source), mirroring what CustomControls/Protocol
    ''' Elements/Materials/SolventContent.xaml displays in its default (non-design) view - the design view's
    ''' "vq"/"mv" shorthand is specific to that view's primary equivalents column, not used here. As with
    ''' reagents, always uses this one canonical form regardless of IsDesignView, and omits parameters that
    ''' aren't actually present rather than leaving blank separators.
    ''' </summary>
    '''
    Private Shared Function BuildSolventContent(name As String, source As String, milliliters As Double, density As Double?,
        isDisplayAsWeight As Boolean, isMolEquivalents As Boolean, equivalents As Double) As String

        Dim content As New Text.StringBuilder()
        AppendAmountUnitPrefix(content, GetSolventVolumeScale(milliliters, density, isDisplayAsWeight))
        content.Append(name)

        Dim detailParts As New List(Of String)

        If equivalents <> 0 Then
            Dim equivUnitText = If(isMolEquivalents, EquivUnit.molEquiv.ToString, EquivUnit.volEquiv.ToString)
            detailParts.Add(ELNCalculations.SignificantDigitsString(equivalents, 3) + " " + equivUnitText)
        End If

        If Not String.IsNullOrEmpty(source) Then
            detailParts.Add(source)
        End If

        AppendDetailParts(content, detailParts)
        Return content.ToString()

    End Function


    ''' <summary>
    ''' Gets the fitting volume/weight amount+unit for a solvent, mirroring VolumeUnitConverter.Convert's core
    ''' logic (CustomControls/Converters/AmountUnitConverters.vb) minus its WPF-binding-specific plumbing.
    ''' </summary>
    '''
    Private Shared Function GetSolventVolumeScale(milliliters As Double, density As Double?,
        isDisplayAsWeight As Boolean) As ELNCalculations.ScaleResult

        If Not isDisplayAsWeight Then
            Return ELNCalculations.ScaleVolume(milliliters)
        End If

        If Not density.HasValue Then
            Return Nothing
        End If

        Return ELNCalculations.ScaleWeight(milliliters * density.Value)

    End Function


    ''' <summary>
    ''' Builds an auxiliary's searchable content, e.g. "120 mg Silicagel (1.52 wtEquiv; Merck 12345)" - the weighed
    ''' amount+unit, name, and (equivalents; source), mirroring what CustomControls/Protocol Elements/Materials/
    ''' AuxiliaryContent.xaml displays in its default (non-design) view. Reuses
    ''' GetReagentWeightScale with isMolarity always False.
    ''' </summary>
    '''
    Private Shared Function BuildAuxiliaryContent(name As String, source As String, grams As Double, density As Double?,
        isDisplayAsVolume As Boolean, equivalents As Double) As String

        Dim content As New Text.StringBuilder()
        AppendAmountUnitPrefix(content, GetReagentWeightScale(grams, density, isDisplayAsVolume, isMolarity:=False))
        content.Append(name)

        Dim detailParts As New List(Of String)

        If equivalents <> 0 Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(equivalents, 3) + " " + EquivUnit.wtEquiv.ToString)
        End If

        If Not String.IsNullOrEmpty(source) Then
            detailParts.Add(source)
        End If

        AppendDetailParts(content, detailParts)
        Return content.ToString()

    End Function


    ''' <summary>
    ''' Builds a reference reactant's searchable content, e.g. "3.84 g Reactant A (1.25 equiv; Merck 12345; 250 mmol;
    ''' 98.5%; 120 mmol/g resin)" - the weighed amount+unit, name, and every other parameter
    ''' CustomControls/Protocol Elements/Materials/RefReactantContent.xaml displays. Identical field set to
    ''' BuildReagentContent minus molarity (reference reactants have no molar-solution concept, hence
    ''' GetReagentWeightScale is called with isMolarity always False, mirroring RefReactantContent.xaml's own
    ''' WeightUnitConverter binding, which hardcodes that same "0" as its 4th MultiBinding value).
    ''' </summary>
    '''
    Private Shared Function BuildRefReactantContent(name As String, source As String, grams As Double, density As Double?,
        isDisplayAsVolume As Boolean, equivalents As Double, mMols As Double, purity As Double?, resinLoad As Double?) As String

        Dim content As New Text.StringBuilder()
        AppendAmountUnitPrefix(content, GetReagentWeightScale(grams, density, isDisplayAsVolume, isMolarity:=False))
        content.Append(name)

        AppendDetailParts(content, BuildMaterialDetailParts(equivalents, source, mMols, purity, resinLoad))
        Return content.ToString()

    End Function


    ''' <summary>
    ''' Builds a product's searchable content, e.g. "12.5 g ProductName (97.5% yield; 98.5% purity; 120 mmol/g
    ''' resin; MW 234.35; EM 234.39; C5H8O2)" - the weighed amount+unit, name, and every other parameter
    ''' CustomControls/Protocol Elements/Materials/ProductContent.xaml displays (yield, purity, resin load,
    ''' molecular weight, exact mass, elemental formula). The raw ElementalFormula string is indexed directly -
    ''' ElementalFormulaConverter only reformats it for on-screen subscript rendering, the underlying text is the
    ''' same either way. Doesn't include the "A:"/"B:"/"C:" ProductIndex letter, since that's a positional display
    ''' label, not searchable content. Yield and MolecularWeight have no nullable "unset" representation (unlike
    ''' Purity/ExactMass/ResinLoad) but default to 0 before a product is actually worked up, so - unlike the XAML,
    ''' which always renders them - both are only included once the product has an actual weighed amount (Grams).
    ''' </summary>
    '''
    Private Shared Function BuildProductContent(name As String, grams As Double, yield As Double, molecularWeight As Double,
        exactMass As Double?, elementalFormula As String, purity As Double?, resinLoad As Double?) As String

        Dim content As New Text.StringBuilder()

        Dim weightScale = ELNCalculations.ScaleWeight(grams)
        AppendAmountUnitPrefix(content, weightScale)
        content.Append(name)

        Dim detailParts As New List(Of String)

        If weightScale IsNot Nothing Then
            detailParts.Add(FormatYieldPercent(yield) + " yield")
        End If

        If purity.HasValue Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(purity.Value, 3) + "% purity")
        End If

        If resinLoad.HasValue Then
            detailParts.Add(ELNCalculations.SignificantDigitsString(resinLoad.Value, 3) + " mmol/g resin")
        End If

        If molecularWeight <> 0 Then
            detailParts.Add("MW " + Format(molecularWeight, "0.00"))
        End If

        If exactMass.HasValue Then
            detailParts.Add("EM " + Format(exactMass.Value, "0.00"))
        End If

        If Not String.IsNullOrEmpty(elementalFormula) Then
            detailParts.Add(elementalFormula)
        End If

        AppendDetailParts(content, detailParts)
        Return content.ToString()

    End Function


    ''' <summary>
    ''' Formats a yield fraction as a percentage string, mirroring YieldConverter.Convert's core logic
    ''' (CustomControls/Converters/AmountUnitConverters.vb) - 3 significant digits above 10%, 2 below (diminished
    ''' precision for very low yields).
    ''' </summary>
    '''
    Private Shared Function FormatYieldPercent(yield As Double) As String

        Dim sigDigits = If(yield > 10, 3, 2)
        Return ELNCalculations.SignificantDigitsString(yield, sigDigits) + "%"

    End Function


    ''' <summary>
    ''' Gets the searchable ProtocolItemID/Content for the given entity if it belongs to one of the protocol
    ''' item satellite tables that feed the full-text SearchIndex, or Nothing if it doesn't.
    ''' </summary>
    ''' <remarks>
    ''' Walks up the entity's actual runtime type hierarchy rather than looking up entity.GetType() directly,
    ''' since lazy-loading proxies (Castle.Proxies.tblReagentsProxy etc. - confirmed for any entity that was
    ''' loaded from the database, which in practice means every Modified/Deleted entity, not just newly-Added
    ''' ones) are runtime subclasses of the real entity type, one level down, not the entity type itself. A
    ''' dictionary lookup keyed by exact type alone would silently never match those, breaking indexing for
    ''' edits to existing rows while incorrectly appearing to work fine for brand-new ones in casual testing.
    ''' </remarks>
    '''
    Private Shared Function GetSearchableRow(entity As Object) As SearchableRow

        Dim dispatch = SearchableEntityProjections.Value
        Dim entityType = entity.GetType()

        While entityType IsNot Nothing AndAlso entityType IsNot GetType(Object)

            Dim projection As Func(Of Object, SearchableRow) = Nothing
            If dispatch.TryGetValue(entityType, projection) Then
                Return projection(entity)
            End If

            entityType = entityType.BaseType

        End While

        If TypeOf entity Is tblReagents Then
            Dim reagent = DirectCast(entity, tblReagents)
            Return New SearchableRow With {
                .ProtocolItemID = reagent.ProtocolItemID,
                .Content = BuildReagentContent(reagent.Name, reagent.Source, reagent.Grams, reagent.Density,
                    CBool(reagent.IsDisplayAsVolume), CBool(reagent.IsMolarity), reagent.Equivalents, reagent.Molarity,
                    reagent.MMols, reagent.Purity, reagent.ResinLoad)
            }
        End If

        If TypeOf entity Is tblSolvents Then
            Dim solvent = DirectCast(entity, tblSolvents)
            Return New SearchableRow With {
                .ProtocolItemID = solvent.ProtocolItemID,
                .Content = BuildSolventContent(solvent.Name, solvent.Source, solvent.Milliliters, solvent.Density,
                    CBool(solvent.IsDisplayAsWeight), CBool(solvent.IsMolEquivalents), solvent.Equivalents)
            }
        End If

        If TypeOf entity Is tblAuxiliaries Then
            Dim auxiliary = DirectCast(entity, tblAuxiliaries)
            Return New SearchableRow With {
                .ProtocolItemID = auxiliary.ProtocolItemID,
                .Content = BuildAuxiliaryContent(auxiliary.Name, auxiliary.Source, auxiliary.Grams, auxiliary.Density,
                    CBool(auxiliary.IsDisplayAsVolume), auxiliary.Equivalents)
            }
        End If

        If TypeOf entity Is tblProducts Then
            Dim product = DirectCast(entity, tblProducts)
            Return New SearchableRow With {
                .ProtocolItemID = product.ProtocolItemID,
                .Content = BuildProductContent(product.Name, product.Grams, product.Yield, product.MolecularWeight,
                    product.ExactMass, product.ElementalFormula, product.Purity, product.ResinLoad)
            }
        End If

        If TypeOf entity Is tblRefReactants Then
            Dim refReactant = DirectCast(entity, tblRefReactants)
            Return New SearchableRow With {
                .ProtocolItemID = refReactant.ProtocolItemID,
                .Content = BuildRefReactantContent(refReactant.Name, refReactant.Source, refReactant.Grams, refReactant.Density,
                    CBool(refReactant.IsDisplayAsVolume), refReactant.Equivalents, refReactant.MMols, refReactant.Purity, refReactant.ResinLoad)
            }
        End If

        If TypeOf entity Is tblComments Then
            Dim comment = DirectCast(entity, tblComments)
            Return New SearchableRow With {.ProtocolItemID = comment.ProtocolItemID, .Content = ExtractPlainText(comment.CommentFlowDoc)}
        End If

        Return Nothing

    End Function


    ''' <summary>
    ''' Rebuilds the persisted tblSearchIndex table from scratch based on the current contents of all protocol
    ''' item satellite tables. Used for the one-time initial backfill after tblSearchIndex is first created (see
    ''' MainWindow's startup sequence), and as a manual repair option should the incremental index ever be
    ''' suspected to have drifted. Does NOT touch the in-memory FTS5 SearchIndex table directly - callers refresh
    ''' that separately afterwards via <see cref="HydrateMemoryIndex"/>.
    ''' </summary>
    '''
    Public Shared Sub RebuildSearchIndex(searchContext As ElnDbContext)

        If Not searchContext.Database.IsSqlite() Then
            Throw New NotSupportedException("The full-text SearchIndex only exists on the local SQLite database.")
        End If

        Dim experimentIDsByProtocolItem = searchContext.tblProtocolItems.AsNoTracking().
            ToDictionary(Function(pi) pi.GUID, Function(pi) pi.ExperimentID)

        'each satellite table is queried through its own EF-translatable projection (declared once, above,
        'and shared with the incremental path) - so the generated SQL only ever fetches the columns actually
        'used for indexing, never an unreferenced BLOB column such as tblEmbeddedFiles.FileBytes/IconImage.

        Dim allRows As New List(Of SearchableRow)

        'tblReagents, tblSolvents and tblAuxiliaries can't share the translatable-expression approach above -
        'their content combines several columns via ELNCalculations scaling logic that isn't SQL-translatable.
        'Still keep each SQL projection down to just the columns actually needed, same as tblComments below.

        allRows.AddRange(searchContext.tblReagents.AsNoTracking().
            Select(Function(r) New With {r.ProtocolItemID, r.Name, r.Source, r.Grams, r.Density, r.IsDisplayAsVolume, r.IsMolarity,
                r.Equivalents, r.Molarity, r.MMols, r.Purity, r.ResinLoad}).
            AsEnumerable().
            Select(Function(r) New SearchableRow With {
                .ProtocolItemID = r.ProtocolItemID,
                .Content = BuildReagentContent(r.Name, r.Source, r.Grams, r.Density, CBool(r.IsDisplayAsVolume), CBool(r.IsMolarity),
                    r.Equivalents, r.Molarity, r.MMols, r.Purity, r.ResinLoad)
            }))

        allRows.AddRange(searchContext.tblSolvents.AsNoTracking().
            Select(Function(s) New With {s.ProtocolItemID, s.Name, s.Source, s.Milliliters, s.Density, s.IsDisplayAsWeight,
                s.IsMolEquivalents, s.Equivalents}).
            AsEnumerable().
            Select(Function(s) New SearchableRow With {
                .ProtocolItemID = s.ProtocolItemID,
                .Content = BuildSolventContent(s.Name, s.Source, s.Milliliters, s.Density, CBool(s.IsDisplayAsWeight),
                    CBool(s.IsMolEquivalents), s.Equivalents)
            }))

        allRows.AddRange(searchContext.tblAuxiliaries.AsNoTracking().
            Select(Function(a) New With {a.ProtocolItemID, a.Name, a.Source, a.Grams, a.Density, a.IsDisplayAsVolume, a.Equivalents}).
            AsEnumerable().
            Select(Function(a) New SearchableRow With {
                .ProtocolItemID = a.ProtocolItemID,
                .Content = BuildAuxiliaryContent(a.Name, a.Source, a.Grams, a.Density, CBool(a.IsDisplayAsVolume), a.Equivalents)
            }))

        allRows.AddRange(searchContext.tblProducts.AsNoTracking().
            Select(Function(p) New With {p.ProtocolItemID, p.Name, p.Grams, p.Yield, p.MolecularWeight, p.ExactMass, p.ElementalFormula,
                p.Purity, p.ResinLoad}).
            AsEnumerable().
            Select(Function(p) New SearchableRow With {
                .ProtocolItemID = p.ProtocolItemID,
                .Content = BuildProductContent(p.Name, p.Grams, p.Yield, p.MolecularWeight, p.ExactMass, p.ElementalFormula, p.Purity, p.ResinLoad)
            }))

        allRows.AddRange(searchContext.tblRefReactants.AsNoTracking().
            Select(Function(r) New With {r.ProtocolItemID, r.Name, r.Source, r.Grams, r.Density, r.IsDisplayAsVolume, r.Equivalents,
                r.MMols, r.Purity, r.ResinLoad}).
            AsEnumerable().
            Select(Function(r) New SearchableRow With {
                .ProtocolItemID = r.ProtocolItemID,
                .Content = BuildRefReactantContent(r.Name, r.Source, r.Grams, r.Density, CBool(r.IsDisplayAsVolume), r.Equivalents,
                    r.MMols, r.Purity, r.ResinLoad)
            }))
        allRows.AddRange(searchContext.tblSeparators.AsNoTracking().Select(SeparatorProjection))
        allRows.AddRange(searchContext.tblEmbeddedFiles.AsNoTracking().Select(EmbeddedFileProjection))

        'tblComments can't share the translatable-expression approach above - extracting plain text from its
        'FlowDocument XAML (via WPF's XamlReader) can only run client-side, not inside a SQL query. Still keep
        'its own SQL projection down to just (ProtocolItemID, CommentFlowDoc) rather than the full entity.

        allRows.AddRange(searchContext.tblComments.AsNoTracking().
            Select(Function(c) New With {c.ProtocolItemID, c.CommentFlowDoc}).
            AsEnumerable().
            Select(Function(c) New SearchableRow With {.ProtocolItemID = c.ProtocolItemID, .Content = ExtractPlainText(c.CommentFlowDoc)}))

        'clear any existing rows (relevant for the manual-repair case; a no-op for the common one-time-backfill
        'case, where tblSearchIndex starts out empty) and replace them with the freshly computed set. A single
        'SaveChanges call below makes this atomic - no separate manual transaction needed here.

        searchContext.tblSearchIndex.RemoveRange(searchContext.tblSearchIndex)

        searchContext.tblSearchIndex.AddRange(allRows.Select(Function(row) New tblSearchIndex With {
            .ProtocolItemID = row.ProtocolItemID,
            .ExperimentID = experimentIDsByProtocolItem.GetValueOrDefault(row.ProtocolItemID),
            .Content = row.Content
        }))

        searchContext.SaveChanges()

    End Sub


    ''' <summary>
    ''' Represents a pending change to the full-text SearchIndex, derived from a single added, modified or
    ''' deleted protocol item satellite entity (reagent, product, solvent, comment, etc.).
    ''' </summary>
    '''
    Friend Class SearchIndexOp

        Public Property ProtocolItemID As String
        Public Property ExperimentID As String
        Public Property Content As String
        Public Property IsDelete As Boolean

    End Class


    ''' <summary>
    ''' Builds the list of SearchIndex changes required for the given added, modified and deleted entities of
    ''' the current unit of work. Only entities belonging to a protocol item satellite table are considered.
    ''' Called by ElnDbContext.SaveChanges before persisting, while added/modified/deleted entity values and
    ''' in-memory relationship fixup (for same-unit-of-work parents) are still available.
    ''' </summary>
    '''
    Friend Shared Function CollectSearchIndexOps(searchContext As ElnDbContext, added As IEnumerable(Of Object),
        modified As IEnumerable(Of Object), deleted As IEnumerable(Of Object)) As List(Of SearchIndexOp)

        Dim ops As New List(Of SearchIndexOp)

        For Each entity In added.Concat(modified)

            Dim row = GetSearchableRow(entity)
            If row IsNot Nothing Then
                ops.Add(New SearchIndexOp With {
                    .ProtocolItemID = row.ProtocolItemID,
                    .ExperimentID = GetExperimentIDForProtocolItem(searchContext, row.ProtocolItemID),
                    .Content = row.Content,
                    .IsDelete = False
                })
            End If

        Next

        For Each entity In deleted

            Dim row = GetSearchableRow(entity)
            If row IsNot Nothing Then
                ops.Add(New SearchIndexOp With {.ProtocolItemID = row.ProtocolItemID, .IsDelete = True})
            End If

        Next

        Return ops

    End Function


    ''' <summary>
    ''' Stages the persisted tblSearchIndex mirror row for each previously collected SearchIndex change, via
    ''' ordinary EF Add/Remove/property mutation - deliberately NOT SaveChanges'd here. Called by
    ''' ElnDbContext.SaveChanges before its own SyncState-assignment/tombstone loop runs, so that loop (which
    ''' re-reads the ChangeTracker fresh at that later point) picks up these staged tblSearchIndex entries
    ''' automatically and treats them exactly like any other satellite table edit - same SyncState/tombstone
    ''' logic, no duplicated code here.
    ''' </summary>
    '''
    Friend Shared Sub StageSearchIndexEntityChanges(searchContext As ElnDbContext, ops As List(Of SearchIndexOp))

        For Each op In ops

            Dim existing = searchContext.tblSearchIndex.Find(op.ProtocolItemID)

            If op.IsDelete Then

                If existing IsNot Nothing Then
                    searchContext.tblSearchIndex.Remove(existing)
                End If

            ElseIf existing IsNot Nothing Then

                existing.ExperimentID = op.ExperimentID
                existing.Content = op.Content

            Else

                searchContext.tblSearchIndex.Add(New tblSearchIndex With {
                    .ProtocolItemID = op.ProtocolItemID,
                    .ExperimentID = op.ExperimentID,
                    .Content = op.Content
                })

            End If

        Next

    End Sub


    ''' <summary>
    ''' Applies previously collected SearchIndex changes to the in-memory FTS5 table. Every change is a
    ''' delete-then-(re)insert keyed by ProtocolItemID, since FTS5 has no natural upsert and the table has no
    ''' other unique constraint to rely on. Called by ElnDbContext.SaveChanges after persisting. Runs on
    ''' MemoryIndexConnection - a separate connection/database from the on-disk one, so this is deliberately
    ''' outside the on-disk SaveChanges transaction; that's fine, since the in-memory index is a derived cache
    ''' rebuilt fresh every session (see HydrateMemoryIndex), not a durability-critical store.
    ''' </summary>
    '''
    Friend Shared Sub ApplyMemoryIndexOps(ops As List(Of SearchIndexOp))

        If ops Is Nothing OrElse ops.Count = 0 Then
            Exit Sub
        End If

        Using command = MemoryIndexConnection.CreateCommand()

            For Each op In ops

                command.CommandText = "DELETE FROM mem.SearchIndex WHERE ProtocolItemID = @id"
                command.Parameters.Clear()
                command.Parameters.AddWithValue("@id", op.ProtocolItemID)
                command.ExecuteNonQuery()

                If Not op.IsDelete Then
                    command.CommandText = "INSERT INTO mem.SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES (@id, @exp, @content)"
                    command.Parameters.Clear()
                    command.Parameters.AddWithValue("@id", op.ProtocolItemID)
                    command.Parameters.AddWithValue("@exp", op.ExperimentID)
                    command.Parameters.AddWithValue("@content", op.Content)
                    command.ExecuteNonQuery()
                End If

            Next

        End Using

    End Sub


    ''' <summary>
    ''' FlowDocument elements that represent an actual content break (a different paragraph, list item, table
    ''' cell, etc.), warranting a space when concatenating their text with their surroundings. Everything else
    ''' (Run, Span, Bold, Italic, Hyperlink, and critically the extra Run elements WPF uses for chemical-formula
    ''' sub/superscripts via BaselineAlignment) is inline formatting that must stay seamlessly concatenated with
    ''' its neighbors - otherwise e.g. "NH4Cl" would incorrectly split into separate "NH", "4", "Cl" tokens.
    ''' </summary>
    '''
    Private Shared ReadOnly BlockLevelFlowDocumentElements As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
        "Paragraph", "List", "ListItem", "Section", "Table", "TableRowGroup", "TableRow", "TableCell", "BlockUIContainer", "LineBreak"
    }


    ''' <summary>
    ''' Converts a comment's FlowDocument XAML into its plain-text content, for indexing purposes.
    ''' </summary>
    ''' <remarks>
    ''' Walks the XAML as plain XML (XDocument) instead of using Markup.XamlReader.Parse + TextRange, which
    ''' builds a full WPF FlowDocument object graph. Measured ~35x slower over this app's real comment data.
    ''' </remarks>
    '''
    Private Shared Function ExtractPlainText(flowDocXaml As String) As String

        If String.IsNullOrEmpty(flowDocXaml) Then
            Return String.Empty
        End If

        Try
            Dim root = XDocument.Parse(flowDocXaml).Root
            If root Is Nothing Then
                Return String.Empty
            End If

            Dim content As New Text.StringBuilder()
            AppendFlowDocumentText(root, content)
            Return content.ToString()

        Catch
            Return String.Empty
        End Try

    End Function


    ''' <summary>
    ''' Recursively appends the text content of a FlowDocument XAML element (and its descendants) to the given
    ''' StringBuilder, inserting a separating space around block-level elements only (see
    ''' BlockLevelFlowDocumentElements) so words never merge across a paragraph/list-item/etc. boundary, while
    ''' inline formatting spans stay seamlessly concatenated with their surrounding text.
    ''' </summary>
    '''
    Private Shared Sub AppendFlowDocumentText(element As XElement, content As Text.StringBuilder)

        For Each node In element.Nodes()

            Dim textNode = TryCast(node, XText)
            If textNode IsNot Nothing Then
                content.Append(textNode.Value)
                Continue For
            End If

            Dim childElement = TryCast(node, XElement)
            If childElement IsNot Nothing Then

                Dim isBlockLevel = BlockLevelFlowDocumentElements.Contains(childElement.Name.LocalName)

                If isBlockLevel Then
                    content.Append(" "c)
                End If

                AppendFlowDocumentText(childElement, content)

                If isBlockLevel Then
                    content.Append(" "c)
                End If

            End If

        Next

    End Sub


    ''' <summary>
    ''' Gets the owning ExperimentID for the given ProtocolItemID. Checks the change tracker first, since the
    ''' parent protocol item may have been added or modified within the same still-uncommitted unit of work.
    ''' </summary>
    '''
    Private Shared Function GetExperimentIDForProtocolItem(searchContext As ElnDbContext, protocolItemID As String) As String

        Dim trackedParent = searchContext.ChangeTracker.Entries(Of tblProtocolItems)().
            FirstOrDefault(Function(e) e.Entity.GUID = protocolItemID AndAlso e.State <> EntityState.Detached)

        If trackedParent IsNot Nothing Then
            Return trackedParent.Entity.ExperimentID
        End If

        Return searchContext.tblProtocolItems.AsNoTracking().
            Where(Function(pi) pi.GUID = protocolItemID).
            Select(Function(pi) pi.ExperimentID).
            FirstOrDefault()

    End Function


    ''' <summary>
    ''' Marker characters wrapped around each matched term within <see cref="ExperimentSearchHit.Snippet"/>.
    ''' Non-printable control characters are used (rather than e.g. HTML-like tags) so they can never collide
    ''' with real indexed content. Consumers (e.g. a WPF attached property rendering the snippet) split on
    ''' these to alternate between plain and highlighted runs.
    ''' </summary>
    '''
    Public Shared ReadOnly HighlightStartMarker As Char = ChrW(1)
    Public Shared ReadOnly HighlightEndMarker As Char = ChrW(2)


    ''' <summary>
    ''' Upper bound on the number of experiments returned by <see cref="SearchExperiments"/>, so an overly
    ''' broad search term (e.g. a common word like "and" or "the") can't return every experiment in the
    ''' database. Once more experiments than this match, the lowest-relevance ones are cut off and
    ''' <see cref="ExperimentSearchResult.WasTruncated"/> is set so the caller can inform the user.
    ''' </summary>
    '''
    Public Shared ReadOnly MaxDisplayedResults As Integer = 200


    ''' <summary>
    ''' Gets all experiments containing at least one protocol item matching the specified search term, ordered
    ''' by relevance (best match first). The whole search term is matched as a single literal phrase - e.g.
    ''' searching "blue water" only finds experiments where those words occur adjacent to each other and in
    ''' that order, not experiments where "blue" and "water" merely occur somewhere independently. The last word
    ''' of the search term is matched as a prefix, so a still-being-typed or deliberately partial last word (e.g.
    ''' "Allyl") still finds it as part of a longer indexed word (e.g. a reagent named "Allyltrimethylsilane") -
    ''' every earlier word in a multi-word search still has to match a complete word. Results are capped at
    ''' <see cref="MaxDisplayedResults"/>.
    ''' </summary>
    ''' <param name="searchTerm">Free-text search term, matched as one literal phrase with the last word as a prefix.</param>
    ''' <param name="searchContext">Local SQLite database context to query.</param>
    '''
    Public Function SearchExperiments(searchTerm As String, searchContext As ElnDbContext) As ExperimentSearchResult

        If String.IsNullOrWhiteSpace(searchTerm) Then
            Return New ExperimentSearchResult With {.Hits = New List(Of ExperimentSearchHit), .WasTruncated = False}
        End If

        If Not searchContext.Database.IsSqlite() Then
            'the MySQL server side uses native FULLTEXT indexes instead of an FTS5 SearchIndex table - not yet implemented
            Throw New NotSupportedException("Full-text search is currently only implemented for the local SQLite database.")
        End If

        Dim rankedExperiments = GetRankedExperimentIDs(searchTerm)

        'cut off the least relevant experiments before even querying tblExperiments for them, rather than
        'truncating the final result list - both cheaper and simpler, since ranking order is already established.
        Dim wasTruncated = rankedExperiments.Count > MaxDisplayedResults
        If wasTruncated Then
            rankedExperiments = rankedExperiments.Take(MaxDisplayedResults).ToList()
        End If

        Dim experimentsByID = searchContext.tblExperiments.
            Where(Function(exp) rankedExperiments.Select(Function(r) r.ExperimentID).Contains(exp.ExperimentID)).
            ToDictionary(Function(exp) exp.ExperimentID)

        'preserve the relevance ranking order - the LINQ query above does not guarantee result order
        Dim hits = rankedExperiments.
            Where(Function(r) experimentsByID.ContainsKey(r.ExperimentID)).
            Select(Function(r) New ExperimentSearchHit With {
                .Experiment = experimentsByID(r.ExperimentID),
                .Snippet = r.Snippet
            }).ToList()

        Return New ExperimentSearchResult With {.Hits = hits, .WasTruncated = wasTruncated}

    End Function


    ''' <summary>
    ''' A single SearchIndex hit: the matching protocol item, its owning experiment, the bm25 relevance rank
    ''' of that item (lower/more negative values are more relevant), and a short highlighted excerpt of its
    ''' content with the matched phrase wrapped in <see cref="HighlightStartMarker"/>/<see cref="HighlightEndMarker"/>.
    ''' </summary>
    '''
    Private Class RankedHit

        Public Property ProtocolItemID As String
        Public Property ExperimentID As String
        Public Property Rank As Double
        Public Property Snippet As String

    End Class


    ''' <summary>
    ''' A single ranked search result: an experiment and a representative highlighted excerpt from its
    ''' best-matching protocol item.
    ''' </summary>
    '''
    Private Class RankedExperiment

        Public Property ExperimentID As String
        Public Property Snippet As String

    End Class


    ''' <summary>
    ''' Gets the experiments matching the search term as a single literal phrase, ordered by relevance (best
    ''' match first), each with a representative highlighted excerpt.
    ''' </summary>
    '''
    Private Function GetRankedExperimentIDs(searchTerm As String) As List(Of RankedExperiment)

        Dim hits = GetRankedHits(QuotePhrase(searchTerm))

        If hits.Count = 0 Then
            Return New List(Of RankedExperiment)
        End If

        'an experiment's overall relevance is the SUM of the relevance of every matching protocol item, not
        'just its single best one - an experiment where the phrase occurs in several items should rank above
        'one where it only occurs once, even if that single occurrence is individually a slightly stronger match.
        Dim totalRankByExperiment As New Dictionary(Of String, Double)
        Dim bestRankByExperiment As New Dictionary(Of String, Double)
        Dim bestSnippetByExperiment As New Dictionary(Of String, String)

        For Each hit In hits

            totalRankByExperiment(hit.ExperimentID) = totalRankByExperiment.GetValueOrDefault(hit.ExperimentID, 0.0) + hit.Rank

            'the representative snippet shown for an experiment comes from its single best-ranked matching
            'item - concatenating every matching item's snippet would defeat the point of a *compact* preview.
            If Not bestRankByExperiment.ContainsKey(hit.ExperimentID) OrElse hit.Rank < bestRankByExperiment(hit.ExperimentID) Then
                bestRankByExperiment(hit.ExperimentID) = hit.Rank
                bestSnippetByExperiment(hit.ExperimentID) = hit.Snippet
            End If

        Next

        Return totalRankByExperiment.Keys.
            OrderBy(Function(id) totalRankByExperiment(id)).
            Select(Function(id) New RankedExperiment With {.ExperimentID = id, .Snippet = bestSnippetByExperiment(id)}).
            ToList()

    End Function


    ''' <summary>
    ''' Runs the phrase FTS5 MATCH query against the in-memory SearchIndex table (via MemoryIndexConnection,
    ''' kept open for the whole app session), returning one entry per matching protocol item together with its
    ''' owning experiment, bm25 relevance rank, and a short highlighted excerpt (12 tokens, matched phrase
    ''' wrapped in <see cref="HighlightStartMarker"/>/<see cref="HighlightEndMarker"/>).
    ''' </summary>
    '''
    Private Shared Function GetRankedHits(quotedPhrase As String) As List(Of RankedHit)

        Dim hits As New List(Of RankedHit)

        Using command = MemoryIndexConnection.CreateCommand()

            'Content is column index 2 in the SearchIndex table (0=ProtocolItemID, 1=ExperimentID, 2=Content).
            'bm25()/snippet()/MATCH's left-hand side must reference the table unqualified - FTS5 parses that
            'position specially and a schema-qualified "mem.SearchIndex" errors there ("no such column"), even
            'though the ordinary FROM clause below is fine (and needs to be) qualified. Unqualified resolution
            'is unambiguous since SearchIndex only ever exists in the "mem" schema, never in "main".
            command.CommandText =
                $"SELECT ProtocolItemID, ExperimentID, bm25(SearchIndex), snippet(SearchIndex, 2, char({AscW(HighlightStartMarker)}), char({AscW(HighlightEndMarker)}), '…', 12) " +
                "FROM mem.SearchIndex WHERE SearchIndex MATCH @term"

            Dim param = command.CreateParameter()
            param.ParameterName = "@term"
            param.Value = quotedPhrase
            command.Parameters.Add(param)

            Using reader = command.ExecuteReader()
                While reader.Read()
                    hits.Add(New RankedHit With {
                        .ProtocolItemID = reader.GetString(0),
                        .ExperimentID = reader.GetString(1),
                        .Rank = reader.GetDouble(2),
                        .Snippet = ExtendHighlightPastClosingBrackets(reader.GetString(3))
                    })
                End While
            End Using

        End Using

        Return hits

    End Function


    ''' <summary>
    ''' Brackets snippet() will highlight - keyed by opening character, valued by its closing counterpart.
    ''' </summary>
    '''
    Private Shared ReadOnly ClosingBracketFor As New Dictionary(Of Char, Char) From {{"("c, ")"c}, {"["c, "]"c}, {"{"c, "}"c}}


    ''' <summary>
    ''' Extends each highlighted span in a snippet to include a closing bracket (')', ']', '}') that
    ''' immediately follows it, provided its matching opening bracket already occurs earlier within that same
    ''' span - e.g. a match on "Thickness(0)" highlights the whole "(0)", not just "(0" with the closing
    ''' paren left outside. This is needed because snippet()/highlight() mark the exact matched token span:
    ''' tokenization drops "(" and ")" as separators, so the last real token ends right before the closing
    ''' bracket, which is technically not part of any token. Only a bracket whose opener is already inside the
    ''' span gets pulled in, so an unrelated stray closing bracket right after a match isn't swallowed too.
    ''' </summary>
    '''
    Private Shared Function ExtendHighlightPastClosingBrackets(snippet As String) As String

        If String.IsNullOrEmpty(snippet) Then
            Return snippet
        End If

        Dim result As New Text.StringBuilder(snippet.Length)
        Dim openBrackets As New Stack(Of Char)
        Dim inHighlight = False
        Dim i = 0

        While i < snippet.Length

            Dim c = snippet(i)

            If c = HighlightStartMarker Then

                inHighlight = True
                openBrackets.Clear()
                result.Append(c)

            ElseIf c = HighlightEndMarker Then

                'pull in any immediately-following closing brackets that balance an opener seen earlier
                'within this same highlighted span
                Dim lookaheadIndex = i + 1
                Dim pendingClosers As New Text.StringBuilder()

                While openBrackets.Count > 0 AndAlso lookaheadIndex < snippet.Length AndAlso
                    snippet(lookaheadIndex) = ClosingBracketFor(openBrackets.Peek())

                    pendingClosers.Append(snippet(lookaheadIndex))
                    openBrackets.Pop()
                    lookaheadIndex += 1

                End While

                result.Append(pendingClosers)
                result.Append(c)
                i = lookaheadIndex - 1
                inHighlight = False

            Else

                If inHighlight Then
                    If ClosingBracketFor.ContainsKey(c) Then
                        openBrackets.Push(c)
                    ElseIf openBrackets.Count > 0 AndAlso c = ClosingBracketFor(openBrackets.Peek()) Then
                        'this closing bracket already balances an opener seen earlier within the span
                        '(e.g. the "]" in "(a[b]c)") - pop it back off rather than leaving it as a dangling
                        'opener that a later character would be incorrectly matched against.
                        openBrackets.Pop()
                    End If
                End If
                result.Append(c)

            End If

            i += 1

        End While

        Return result.ToString()

    End Function


    ''' <summary>
    ''' Wraps the whole search term in double quotes as a single FTS5 phrase, escaping any embedded quote
    ''' characters, with a trailing "*" making the last token a prefix match. A phrase query requires its tokens
    ''' to occur adjacent and in that order within the same indexed row - this is what makes a multi-word search
    ''' term match only the literal phrase typed, rather than each word independently. Quoting also avoids FTS5
    ''' query syntax errors for terms containing characters with special meaning to FTS5 (-, *, :, parentheses,
    ''' the AND/OR/NOT keywords, etc). The trailing "*" is FTS5's documented syntax for turning the right-most
    ''' token of a quoted phrase into a prefix query (e.g. "blue water"* matches "blue waterway" too, still
    ''' requiring an exact, adjacent "blue") - without it, every token has to match a complete indexed word, so
    ''' e.g. searching "Allyl" would never find a reagent named "Allyltrimethylsilane", since FTS5 tokenizes that
    ''' name as one single word and a plain phrase query only ever matches whole tokens, never substrings of them.
    ''' </summary>
    '''
    Private Shared Function QuotePhrase(searchTerm As String) As String

        Return """" + searchTerm.Trim().Replace("""", """""") + """*"

    End Function

End Class


''' <summary>
''' A single full-text search result: an experiment and a short excerpt from its best-matching protocol item,
''' with the matched term(s) wrapped in FullTextSearch.HighlightStartMarker/HighlightEndMarker.
''' </summary>
'''
Public Class ExperimentSearchHit

    Public Property Experiment As tblExperiments
    Public Property Snippet As String

End Class


''' <summary>
''' The outcome of a <see cref="FullTextSearch.SearchExperiments"/> call: the (possibly capped) list of
''' matching experiments, and whether more matches existed than <see cref="FullTextSearch.MaxDisplayedResults"/>
''' and were cut off, e.g. because the search term was too broad (a common word like "and" or "the").
''' </summary>
'''
Public Class ExperimentSearchResult

    Public Property Hits As List(Of ExperimentSearchHit)
    Public Property WasTruncated As Boolean

End Class
