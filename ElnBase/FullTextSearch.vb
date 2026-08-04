Imports System.Data
Imports System.Linq.Expressions
Imports System.Xml.Linq
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

''' <summary>
''' Owns everything related to the full-text SearchIndex FTS5 virtual table: its schema, keeping it in sync
''' with the protocol item satellite tables (reagents, products, solvents, auxiliaries, reference reactants,
''' separators, embedded files and comments), and querying it.
''' </summary>
'''
Public Class FullTextSearch

    ''' <summary>
    ''' Name of the full-text SearchIndex FTS5 virtual table. FTS5 backs this with several real shadow tables
    ''' named "SearchIndexTableName_*" (_data, _idx, _content, _docsize, _config) - these, like the virtual
    ''' table itself, are local-SQLite-only and must never be included in server bulk-upload/sync table scans.
    ''' </summary>
    '''
    Friend Shared ReadOnly SearchIndexTableName As String = "SearchIndex"


    ''' <summary>
    ''' DDL for the full-text SearchIndex FTS5 virtual table. Shared between ElnDbContext's constructor
    ''' self-heal check and DbUpgradeLocal's documented, versioned schema history, so the two can't drift apart.
    ''' </summary>
    '''
    Friend Shared ReadOnly SearchIndexTableDDL As String =
        $"CREATE VIRTUAL TABLE IF NOT EXISTS {SearchIndexTableName} USING fts5(ProtocolItemID UNINDEXED, ExperimentID UNINDEXED, Content, " +
        "tokenize=""unicode61 remove_diacritics 2"");"


    ''' <summary>
    ''' Ensures the SearchIndex FTS5 virtual table exists. Called from ElnDbContext's constructor so full-text
    ''' search works regardless of whether DbUpgradeLocal.Upgrade has already run against this particular
    ''' database file (e.g. one just restored from the server, or carried over from an older/foreign installation).
    ''' </summary>
    '''
    Friend Shared Sub EnsureSearchIndexTableExists(searchContext As ElnDbContext)

        searchContext.Database.ExecuteSqlRaw(SearchIndexTableDDL)

    End Sub


    ''' <summary>
    ''' Deletes the full text search index table and its satellites from the specified SqLite database context.
    ''' </summary>
    ''' <remarks> The currently used SqLite editors don't allow this operation manually, since the FTS5 module 
    ''' is missing there.</remarks>
    ''' 
    Public Shared Sub RemoveSearchIndexTable(searchContext As ElnDbContext)

        If searchContext.Database.IsSqlite() Then
            searchContext.Database.ExecuteSqlRaw("DROP TABLE SearchIndex")
        End If

    End Sub


    ''' <summary>
    ''' Gets if the full-text SearchIndex currently contains no entries, e.g. because it was just created by
    ''' a schema upgrade and still needs its initial backfill via <see cref="RebuildSearchIndex"/>.
    ''' </summary>
    '''
    Public Shared Function IsSearchIndexEmpty(searchContext As ElnDbContext) As Boolean

        If Not searchContext.Database.IsSqlite() Then
            Throw New NotSupportedException("The full-text SearchIndex only exists on the local SQLite database.")
        End If

        Return searchContext.Database.SqlQueryRaw(Of Integer)("SELECT EXISTS(SELECT 1 FROM SearchIndex) AS Value").First() = 0

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
    ''' below. tblComments is the one deliberate exception (see RebuildSearchIndex/GetSearchableRow) - extracting
    ''' plain text from its FlowDocument XAML needs WPF's XamlReader, which can only run client-side, never as
    ''' part of a translated SQL query.
    ''' </summary>
    '''
    Private Shared ReadOnly ReagentProjection As Expression(Of Func(Of tblReagents, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Name + " " + item.Source}

    Private Shared ReadOnly ProductProjection As Expression(Of Func(Of tblProducts, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Name}

    Private Shared ReadOnly SolventProjection As Expression(Of Func(Of tblSolvents, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Name + " " + item.Source}

    Private Shared ReadOnly AuxiliaryProjection As Expression(Of Func(Of tblAuxiliaries, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Name + " " + item.Source}

    Private Shared ReadOnly RefReactantProjection As Expression(Of Func(Of tblRefReactants, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Name + " " + item.Source}

    Private Shared ReadOnly SeparatorProjection As Expression(Of Func(Of tblSeparators, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.Title}

    Private Shared ReadOnly EmbeddedFileProjection As Expression(Of Func(Of tblEmbeddedFiles, SearchableRow)) =
        Function(item) New SearchableRow With {.ProtocolItemID = item.ProtocolItemID, .Content = item.FileName + " " + item.FileComment}

    ' tblComments is treated separately in RebuildSearchIndex, since plain text needs to be extracted from xaml


    ''' <summary>
    ''' Compiled, type-erased dispatch table derived from the projections above (never declared separately),
    ''' keyed by entity CLR type. Used by the incremental (already-in-memory) path to find and invoke the right
    ''' projection for a given tracked entity. Built once, lazily, on first use.
    ''' </summary>
    '''
    Private Shared ReadOnly SearchableEntityProjections As New Lazy(Of Dictionary(Of Type, Func(Of Object, SearchableRow)))(
        Function()

            Dim dispatch As New Dictionary(Of Type, Func(Of Object, SearchableRow))

            AddProjection(dispatch, ReagentProjection)
            AddProjection(dispatch, ProductProjection)
            AddProjection(dispatch, SolventProjection)
            AddProjection(dispatch, AuxiliaryProjection)
            AddProjection(dispatch, RefReactantProjection)
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

        If TypeOf entity Is tblComments Then
            Dim comment = DirectCast(entity, tblComments)
            Return New SearchableRow With {.ProtocolItemID = comment.ProtocolItemID, .Content = ExtractPlainText(comment.CommentFlowDoc)}
        End If

        Return Nothing

    End Function


    ''' <summary>
    ''' Rebuilds the full-text SearchIndex from scratch based on the current contents of all protocol item
    ''' satellite tables. Used for the initial backfill after the SearchIndex table is first created, and as a
    ''' manual repair option should the incremental index ever be suspected to have drifted.
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
        allRows.AddRange(searchContext.tblReagents.AsNoTracking().Select(ReagentProjection))
        allRows.AddRange(searchContext.tblProducts.AsNoTracking().Select(ProductProjection))
        allRows.AddRange(searchContext.tblSolvents.AsNoTracking().Select(SolventProjection))
        allRows.AddRange(searchContext.tblAuxiliaries.AsNoTracking().Select(AuxiliaryProjection))
        allRows.AddRange(searchContext.tblRefReactants.AsNoTracking().Select(RefReactantProjection))
        allRows.AddRange(searchContext.tblSeparators.AsNoTracking().Select(SeparatorProjection))
        allRows.AddRange(searchContext.tblEmbeddedFiles.AsNoTracking().Select(EmbeddedFileProjection))

        'tblComments can't share the translatable-expression approach above - extracting plain text from its
        'FlowDocument XAML (via WPF's XamlReader) can only run client-side, not inside a SQL query. Still keep
        'its own SQL projection down to just (ProtocolItemID, CommentFlowDoc) rather than the full entity.

        allRows.AddRange(searchContext.tblComments.AsNoTracking().
            Select(Function(c) New With {c.ProtocolItemID, c.CommentFlowDoc}).
            AsEnumerable().
            Select(Function(c) New SearchableRow With {.ProtocolItemID = c.ProtocolItemID, .Content = ExtractPlainText(c.CommentFlowDoc)}))

        'wrap the whole rebuild in a single transaction - without this, every DELETE/INSERT below runs as its
        'own autocommit transaction and fsyncs individually, which is what actually made this slow (not the
        'FTS5 tokenizing work itself).

        Dim ownsTransaction = (searchContext.Database.CurrentTransaction Is Nothing)
        Dim transaction = If(ownsTransaction, searchContext.Database.BeginTransaction(), searchContext.Database.CurrentTransaction)

        Try

            searchContext.Database.ExecuteSqlRaw("DELETE FROM SearchIndex")

            For Each row In allRows
                searchContext.Database.ExecuteSqlRaw("INSERT INTO SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES ({0}, {1}, {2})",
                    row.ProtocolItemID, experimentIDsByProtocolItem.GetValueOrDefault(row.ProtocolItemID), row.Content)
            Next

            If ownsTransaction Then
                transaction.Commit()
            End If

        Catch

            If ownsTransaction Then
                transaction.Rollback()
            End If
            Throw

        Finally

            If ownsTransaction Then
                transaction.Dispose()
            End If

        End Try

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
    ''' Applies previously collected SearchIndex changes. Every change is a delete-then-(re)insert keyed by
    ''' ProtocolItemID, since FTS5 has no natural upsert and the table has no other unique constraint to rely on.
    ''' Called by ElnDbContext.SaveChanges after persisting, within the same transaction.
    ''' </summary>
    '''
    Friend Shared Sub ApplySearchIndexOps(searchContext As ElnDbContext, ops As List(Of SearchIndexOp))

        For Each op In ops

            searchContext.Database.ExecuteSqlRaw("DELETE FROM SearchIndex WHERE ProtocolItemID = {0}", op.ProtocolItemID)

            If Not op.IsDelete Then
                searchContext.Database.ExecuteSqlRaw("INSERT INTO SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES ({0}, {1}, {2})",
                    op.ProtocolItemID, op.ExperimentID, op.Content)
            End If

        Next

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
    ''' that order, not experiments where "blue" and "water" merely occur somewhere independently. Results are
    ''' capped at <see cref="MaxDisplayedResults"/>.
    ''' </summary>
    ''' <param name="searchTerm">Free-text search term, matched as one literal phrase.</param>
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

        Dim rankedExperiments = GetRankedExperimentIDs(searchContext, searchTerm)

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
    Private Function GetRankedExperimentIDs(searchContext As ElnDbContext, searchTerm As String) As List(Of RankedExperiment)

        Dim hits = GetRankedHits(searchContext, QuotePhrase(searchTerm))

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
    ''' Runs the phrase FTS5 MATCH query, returning one entry per matching protocol item together with its
    ''' owning experiment, bm25 relevance rank, and a short highlighted excerpt (12 tokens, matched phrase
    ''' wrapped in <see cref="HighlightStartMarker"/>/<see cref="HighlightEndMarker"/>).
    ''' </summary>
    '''
    Private Shared Function GetRankedHits(searchContext As ElnDbContext, quotedPhrase As String) As List(Of RankedHit)

        Dim hits As New List(Of RankedHit)

        Using command = searchContext.Database.GetDbConnection().CreateCommand()

            'Content is column index 2 in the SearchIndex table (0=ProtocolItemID, 1=ExperimentID, 2=Content).
            command.CommandText =
                $"SELECT ProtocolItemID, ExperimentID, bm25(SearchIndex), snippet(SearchIndex, 2, char({AscW(HighlightStartMarker)}), char({AscW(HighlightEndMarker)}), '…', 12) " +
                "FROM SearchIndex WHERE SearchIndex MATCH @term"

            Dim param = command.CreateParameter()
            param.ParameterName = "@term"
            param.Value = quotedPhrase
            command.Parameters.Add(param)

            Dim wasClosed = (command.Connection.State <> ConnectionState.Open)
            If wasClosed Then
                command.Connection.Open()
            End If

            Try
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
            Finally
                If wasClosed Then
                    command.Connection.Close()
                End If
            End Try

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
    ''' characters. A phrase query requires its tokens to occur adjacent and in that order within the same
    ''' indexed row - this is what makes a multi-word search term match only the literal phrase typed, rather
    ''' than each word independently. Quoting also avoids FTS5 query syntax errors for terms containing
    ''' characters with special meaning to FTS5 (-, *, :, parentheses, the AND/OR/NOT keywords, etc).
    ''' </summary>
    '''
    Private Shared Function QuotePhrase(searchTerm As String) As String

        Return """" + searchTerm.Trim().Replace("""", """""") + """"

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
