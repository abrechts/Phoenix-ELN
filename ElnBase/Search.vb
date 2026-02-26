Imports ElnCoreModel

Public Class Search


    Public Property SearchContext As ElnDbContext   'assigned rdoLocal and rdoServer setting

    Public Property IsServerQuery As Boolean = False

    Public Property RssError As RssErrorType = RssErrorType.None


    Public Class RSSQueryFilters

        Public Property UserID As String = ""
        Public Property ProjectName As String = ""
        Public Property ExpGroupName As String = ""
        Public Property MinYield As Double = Double.MinValue
        Public Property MaxYield As Double = Double.MaxValue
        Public Property MinScale As Double = Double.MinValue
        Public Property MaxScale As Double = Double.MaxValue
        Public Property MinDate As Date = Date.MinValue
        Public Property MaxDate As Date = Date.MaxValue

    End Class


    Public Sub New(searchContext As ElnDbContext, isServerQuery As Boolean)

        Me.SearchContext = searchContext
        Me.IsServerQuery = isServerQuery

    End Sub


    Public Function FilteredRssQuery(rxnFileStr As String, queryFilters As RSSQueryFilters) As IEnumerable(Of tblExperiments)

        RssError = RssErrorType.None

        ' quit if no reactionfile string is provided, since this query is based on RSS search with applied filters. 
        If String.IsNullOrEmpty(rxnFileStr) Then
            Return Enumerable.Empty(Of tblExperiments)()
        End If

        Dim expHits = SearchContext.tblExperiments.AsQueryable

        With queryFilters

            ' userID 
            If expHits.Any AndAlso Not String.IsNullOrEmpty(.UserID) Then
                expHits = expHits.Where(Function(x) x.UserID = .UserID)
            End If

            ' project name
            If expHits.Any AndAlso Not String.IsNullOrEmpty(.ProjectName) Then
                expHits = expHits.Where(Function(x) x.Project.Title = .ProjectName)
            End If

            ' experiment group name
            If Not String.IsNullOrEmpty(.ExpGroupName) Then
                expHits = expHits.Where(Function(x) x.ProjFolder.FolderName = .ExpGroupName)
            End If

            ' creation date range
            If expHits.Any AndAlso (.MinDate <> Date.MinValue OrElse .MaxDate <> Date.MaxValue) Then
                expHits = expHits.Where(Function(x) x.CreationDate >= .MinDate AndAlso x.CreationDate <= .MaxDate)
            End If

            ' yield range
            If expHits.Any AndAlso (.MinYield <> Double.MinValue OrElse .MaxYield <> Double.MaxValue) Then
                expHits = expHits.Where(Function(x) x.Yield >= .MinYield AndAlso x.Yield <= .MaxYield)
            End If

            ' scale range
            If expHits.Any AndAlso (.MinScale <> Double.MinValue OrElse .MaxScale <> Double.MaxValue) Then
                expHits = expHits.Where(Function(x) x.RefReactantGrams >= .MinScale AndAlso x.RefReactantGrams <= .MaxScale)
            End If

        End With

        ' reaction substructure

        If expHits.Any Then

            Dim rxnSub As New RxnSubstructure
            Dim expHitInfo = rxnSub.PerformRssQuery(rxnFileStr, expHits, IsServerQuery)
            RssError = expHitInfo.ErrorType

            Return expHitInfo.ExperimentHits

        Else

            'test in combination RSSError=None to detect that filter hits were found even before RSS query
            Return Enumerable.Empty(Of tblExperiments)

        End If

    End Function

End Class
