Imports System.Data
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore

''' <summary>
''' Performs full-text search across all protocol item satellite tables (reagents, products, solvents,
''' auxiliaries, reference reactants, separators, embedded files and comments) via the SearchIndex FTS5
''' virtual table maintained by ElnDbContext.SaveChanges/RebuildSearchIndex.
''' </summary>
'''
Public Class FullTextSearch

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
    ''' A single SearchIndex hit: the owning experiment and the bm25 relevance rank of the matching protocol
    ''' item (lower/more negative values are more relevant).
    ''' </summary>
    '''
    Private Class RankedHit

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

        Dim bestRankByID As New Dictionary(Of String, Double)

        For Each hits In hitsPerWord
            For Each hit In hits
                If matchingIds.Contains(hit.ExperimentID) Then
                    If Not bestRankByID.ContainsKey(hit.ExperimentID) OrElse hit.Rank < bestRankByID(hit.ExperimentID) Then
                        bestRankByID(hit.ExperimentID) = hit.Rank
                    End If
                End If
            Next
        Next

        Return matchingIds.OrderBy(Function(id) bestRankByID(id)).ToList()

    End Function


    ''' <summary>
    ''' Runs a single-word FTS5 MATCH query, returning one entry per matching protocol item's owning
    ''' experiment together with that item's bm25 relevance rank.
    ''' </summary>
    '''
    Private Function GetRankedHits(searchContext As ElnDbContext, quotedTerm As String) As List(Of RankedHit)

        Dim hits As New List(Of RankedHit)

        Using command = searchContext.Database.GetDbConnection().CreateCommand()

            command.CommandText = "SELECT ExperimentID, bm25(SearchIndex) FROM SearchIndex WHERE SearchIndex MATCH @term"

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
                        hits.Add(New RankedHit With {.ExperimentID = reader.GetString(0), .Rank = reader.GetDouble(1)})
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
