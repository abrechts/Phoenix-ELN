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
    ''' Marker characters wrapped around each matched term within <see cref="ExperimentSearchHit.Snippet"/>.
    ''' Non-printable control characters are used (rather than e.g. HTML-like tags) so they can never collide
    ''' with real indexed content. Consumers (e.g. a WPF attached property rendering the snippet) split on
    ''' these to alternate between plain and highlighted runs.
    ''' </summary>
    '''
    Public Shared ReadOnly HighlightStartMarker As Char = ChrW(1)
    Public Shared ReadOnly HighlightEndMarker As Char = ChrW(2)


    ''' <summary>
    ''' Gets all experiments containing at least one protocol item matching the specified search term, ordered
    ''' by relevance (best match first). The whole search term is matched as a single literal phrase - e.g.
    ''' searching "blue water" only finds experiments where those words occur adjacent to each other and in
    ''' that order, not experiments where "blue" and "water" merely occur somewhere independently.
    ''' </summary>
    ''' <param name="searchTerm">Free-text search term, matched as one literal phrase.</param>
    ''' <param name="searchContext">Local SQLite database context to query.</param>
    '''
    Public Function SearchExperiments(searchTerm As String, searchContext As ElnDbContext) As IEnumerable(Of ExperimentSearchHit)

        If String.IsNullOrWhiteSpace(searchTerm) Then
            Return Enumerable.Empty(Of ExperimentSearchHit)
        End If

        If Not searchContext.Database.IsSqlite() Then
            'the MySQL server side uses native FULLTEXT indexes instead of an FTS5 SearchIndex table - not yet implemented
            Throw New NotSupportedException("Full-text search is currently only implemented for the local SQLite database.")
        End If

        Dim rankedExperiments = GetRankedExperimentIDs(searchContext, searchTerm)

        Dim experimentsByID = searchContext.tblExperiments.
            Where(Function(exp) rankedExperiments.Select(Function(r) r.ExperimentID).Contains(exp.ExperimentID)).
            ToDictionary(Function(exp) exp.ExperimentID)

        'preserve the relevance ranking order - the LINQ query above does not guarantee result order
        Return rankedExperiments.
            Where(Function(r) experimentsByID.ContainsKey(r.ExperimentID)).
            Select(Function(r) New ExperimentSearchHit With {
                .Experiment = experimentsByID(r.ExperimentID),
                .Snippet = r.Snippet
            })

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
