Imports System.Data
Imports System.Windows
Imports System.Windows.Documents
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
    ''' Rebuilds the full-text SearchIndex from scratch based on the current contents of all protocol item
    ''' satellite tables. Used for the initial backfill after the SearchIndex table is first created, and as a
    ''' manual repair option should the incremental index ever be suspected to have drifted.
    ''' </summary>
    '''
    Public Shared Sub RebuildSearchIndex(searchContext As ElnDbContext)

        If Not searchContext.Database.IsSqlite() Then
            Throw New NotSupportedException("The full-text SearchIndex only exists on the local SQLite database.")
        End If

        searchContext.Database.ExecuteSqlRaw("DELETE FROM SearchIndex")

        Dim experimentIDsByProtocolItem = searchContext.tblProtocolItems.AsNoTracking().
            ToDictionary(Function(pi) pi.GUID, Function(pi) pi.ExperimentID)

        'materialize each satellite table individually first (.ToList()) - chaining .Concat() directly on the
        'IQueryable sources would make EF Core try to translate the whole union into a single incompatible SQL
        'set operation instead of combining them in memory.
        Dim allSatelliteEntities As IEnumerable(Of Object) =
            searchContext.tblReagents.AsNoTracking().ToList().Cast(Of Object)().
            Concat(searchContext.tblProducts.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblSolvents.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblAuxiliaries.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblRefReactants.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblSeparators.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblEmbeddedFiles.AsNoTracking().ToList().Cast(Of Object)()).
            Concat(searchContext.tblComments.AsNoTracking().ToList().Cast(Of Object)())

        For Each entity In allSatelliteEntities

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                searchContext.Database.ExecuteSqlRaw("INSERT INTO SearchIndex(ProtocolItemID, ExperimentID, Content) VALUES ({0}, {1}, {2})",
                    protocolItemID, experimentIDsByProtocolItem.GetValueOrDefault(protocolItemID), GetSearchableContent(entity))
            End If

        Next

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

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                ops.Add(New SearchIndexOp With {
                    .ProtocolItemID = protocolItemID,
                    .ExperimentID = GetExperimentIDForProtocolItem(searchContext, protocolItemID),
                    .Content = GetSearchableContent(entity),
                    .IsDelete = False
                })
            End If

        Next

        For Each entity In deleted

            Dim protocolItemID As String = Nothing
            If TryGetSearchableProtocolItemID(entity, protocolItemID) Then
                ops.Add(New SearchIndexOp With {.ProtocolItemID = protocolItemID, .IsDelete = True})
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
    ''' Gets if the specified entity belongs to one of the protocol item satellite tables that feed the
    ''' full-text SearchIndex, and if so, its owning ProtocolItemID.
    ''' </summary>
    '''
    Private Shared Function TryGetSearchableProtocolItemID(entity As Object, ByRef protocolItemID As String) As Boolean

        Select Case True

            Case TypeOf entity Is tblReagents
                protocolItemID = DirectCast(entity, tblReagents).ProtocolItemID
            Case TypeOf entity Is tblProducts
                protocolItemID = DirectCast(entity, tblProducts).ProtocolItemID
            Case TypeOf entity Is tblSolvents
                protocolItemID = DirectCast(entity, tblSolvents).ProtocolItemID
            Case TypeOf entity Is tblAuxiliaries
                protocolItemID = DirectCast(entity, tblAuxiliaries).ProtocolItemID
            Case TypeOf entity Is tblRefReactants
                protocolItemID = DirectCast(entity, tblRefReactants).ProtocolItemID
            Case TypeOf entity Is tblSeparators
                protocolItemID = DirectCast(entity, tblSeparators).ProtocolItemID
            Case TypeOf entity Is tblEmbeddedFiles
                protocolItemID = DirectCast(entity, tblEmbeddedFiles).ProtocolItemID
            Case TypeOf entity Is tblComments
                protocolItemID = DirectCast(entity, tblComments).ProtocolItemID
            Case Else
                protocolItemID = Nothing
                Return False

        End Select

        Return True

    End Function


    ''' <summary>
    ''' Extracts the plain-text searchable content of a protocol item satellite entity.
    ''' </summary>
    '''
    Private Shared Function GetSearchableContent(entity As Object) As String

        Select Case True

            Case TypeOf entity Is tblReagents
                Dim item = DirectCast(entity, tblReagents)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblProducts
                Return DirectCast(entity, tblProducts).Name
            Case TypeOf entity Is tblSolvents
                Dim item = DirectCast(entity, tblSolvents)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblAuxiliaries
                Dim item = DirectCast(entity, tblAuxiliaries)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblRefReactants
                Dim item = DirectCast(entity, tblRefReactants)
                Return item.Name + " " + item.Source
            Case TypeOf entity Is tblSeparators
                Return DirectCast(entity, tblSeparators).Title
            Case TypeOf entity Is tblEmbeddedFiles
                Dim item = DirectCast(entity, tblEmbeddedFiles)
                Return item.FileName + " " + item.FileComment
            Case TypeOf entity Is tblComments
                Return ExtractPlainText(DirectCast(entity, tblComments).CommentFlowDoc)
            Case Else
                Return String.Empty

        End Select

    End Function


    ''' <summary>
    ''' Converts a comment's FlowDocument XAML into its plain-text content, for indexing purposes.
    ''' </summary>
    '''
    Private Shared Function ExtractPlainText(flowDocXaml As String) As String

        If String.IsNullOrEmpty(flowDocXaml) Then
            Return String.Empty
        End If

        Try
            Dim doc = TryCast(Markup.XamlReader.Parse(flowDocXaml), FlowDocument)
            If doc Is Nothing Then
                Return String.Empty
            End If
            Return New TextRange(doc.ContentStart, doc.ContentEnd).Text
        Catch
            Return String.Empty
        End Try

    End Function


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
    ''' Gets all experiments containing at least one protocol item matching the specified search term, ordered
    ''' by relevance (best match first). Multiple whitespace-separated words in the search term are combined
    ''' with AND, where each word may match in a *different* protocol item of the experiment - e.g. one word
    ''' found in a reagent name and another in an unrelated comment still counts as a match.
    ''' </summary>
    ''' <param name="searchTerm">Free-text search term.</param>
    ''' <param name="searchContext">Local SQLite database context to query.</param>
    '''
    Public Function SearchExperiments(searchTerm As String, searchContext As ElnDbContext) As IEnumerable(Of tblExperiments)

        If String.IsNullOrWhiteSpace(searchTerm) Then
            Return Enumerable.Empty(Of tblExperiments)
        End If

        If Not searchContext.Database.IsSqlite() Then
            'the MySQL server side uses native FULLTEXT indexes instead of an FTS5 SearchIndex table - not yet implemented
            Throw New NotSupportedException("Full-text search is currently only implemented for the local SQLite database.")
        End If

        Dim rankedIds = GetRankedExperimentIDs(searchContext, searchTerm)

        Dim experimentsByID = searchContext.tblExperiments.
            Where(Function(exp) rankedIds.Contains(exp.ExperimentID)).
            ToDictionary(Function(exp) exp.ExperimentID)

        'preserve the relevance ranking order - the LINQ query above does not guarantee result order
        Return rankedIds.
            Where(Function(id) experimentsByID.ContainsKey(id)).
            Select(Function(id) experimentsByID(id))

    End Function


    ''' <summary>
    ''' A single SearchIndex hit: the matching protocol item, its owning experiment, and the bm25 relevance
    ''' rank of that item for one query word (lower/more negative values are more relevant).
    ''' </summary>
    '''
    Private Class RankedHit

        Public Property ProtocolItemID As String
        Public Property ExperimentID As String
        Public Property Rank As Double

    End Class


    ''' <summary>
    ''' Gets the ExperimentIDs matching every word of the search term, ordered by relevance (best match first).
    ''' Each word is run as its own MATCH query and the resulting experiment sets are intersected in memory,
    ''' rather than combining all words into a single MATCH expression - FTS5 would otherwise require every
    ''' word to occur within the very same protocol item's indexed content, which is too strict for searching
    ''' across an experiment's reagents, comments, products etc. as a whole.
    ''' </summary>
    '''
    Private Function GetRankedExperimentIDs(searchContext As ElnDbContext, searchTerm As String) As List(Of String)

        Dim words = searchTerm.Split({" "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)

        If words.Length = 0 Then
            Return New List(Of String)
        End If

        Dim hitsPerWord = words.Select(Function(w) GetRankedHits(searchContext, QuoteTerm(w))).ToList()

        Dim matchingIds = hitsPerWord.
            Select(Function(hits) New HashSet(Of String)(hits.Select(Function(h) h.ExperimentID))).
            Aggregate(Function(setA, setB) New HashSet(Of String)(setA.Intersect(setB)))

        'best (lowest/most relevant) rank achieved by each distinct protocol item, across whichever query
        'word(s) matched it - a row that happens to match more than one word must only count once, not once
        'per word, otherwise multi-word searches would arbitrarily favor items that match many query words
        'within a single row over items that match once but in several different rows.

        Dim bestRankByItem As New Dictionary(Of String, Double)
        Dim experimentIDByItem As New Dictionary(Of String, String)

        For Each hits In hitsPerWord
            For Each hit In hits
                If matchingIds.Contains(hit.ExperimentID) Then
                    If Not bestRankByItem.ContainsKey(hit.ProtocolItemID) OrElse hit.Rank < bestRankByItem(hit.ProtocolItemID) Then
                        bestRankByItem(hit.ProtocolItemID) = hit.Rank
                        experimentIDByItem(hit.ProtocolItemID) = hit.ExperimentID
                    End If
                End If
            Next
        Next

        'an experiment's overall relevance is the SUM of the relevance of every distinct matching protocol
        'item, not just its single best one - otherwise an experiment with several relevant reagents/comments
        'would rank no higher than one with only a single (even if individually stronger) match, which doesn't
        'reflect it being the more thoroughly relevant experiment overall.

        Dim totalRankByExperiment As New Dictionary(Of String, Double)

        For Each kvp In bestRankByItem
            Dim experimentID = experimentIDByItem(kvp.Key)
            totalRankByExperiment(experimentID) = totalRankByExperiment.GetValueOrDefault(experimentID, 0.0) + kvp.Value
        Next

        Return matchingIds.OrderBy(Function(id) totalRankByExperiment(id)).ToList()

    End Function


    ''' <summary>
    ''' Runs a single-word FTS5 MATCH query, returning one entry per matching protocol item together with its
    ''' owning experiment and bm25 relevance rank.
    ''' </summary>
    '''
    Private Shared Function GetRankedHits(searchContext As ElnDbContext, quotedTerm As String) As List(Of RankedHit)

        Dim hits As New List(Of RankedHit)

        Using command = searchContext.Database.GetDbConnection().CreateCommand()

            command.CommandText = "SELECT ProtocolItemID, ExperimentID, bm25(SearchIndex) FROM SearchIndex WHERE SearchIndex MATCH @term"

            Dim param = command.CreateParameter()
            param.ParameterName = "@term"
            param.Value = quotedTerm
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
                            .Rank = reader.GetDouble(2)
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
    ''' Wraps a single word in double quotes for safe use in an FTS5 MATCH expression, escaping any embedded
    ''' quote characters. Quoting avoids FTS5 query syntax errors for words containing characters with special
    ''' meaning to FTS5 (-, *, :, parentheses, the AND/OR/NOT keywords, etc).
    ''' </summary>
    '''
    Private Function QuoteTerm(term As String) As String

        Return """" + term.Replace("""", """""") + """"

    End Function

End Class
