Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnBase.Search
Imports ElnCoreModel

Public Class dlgSearch

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets if the current query is server based (true) or local (false).
    ''' </summary>
    '''
    Public Shared Property IsServerQuery As Boolean = False


    Public Property LocalDBContext As ElnDbContext

    Public Property ServerDBContext As ElnDbContext

    Private Property SearchContext As ElnDbContext   'assigned rdoLocal and rdoServer setting

    Private Property QueryRxnFileString As String

    Private Property _cvsUsers As CollectionViewSource
    Private Property _cvsProjects As CollectionViewSource


    Private Sub Me_Loaded() Handles Me.Loaded

        SearchContext = LocalDBContext

        If ServerDBContext Is Nothing Then
            IsServerQuery = False
            rdoServer.IsEnabled = False
        Else
            rdoServer.IsChecked = IsServerQuery
        End If

        _cvsUsers = Me.FindResource("UsersView")
        _cvsProjects = Me.FindResource("ProjectsView")

        RefreshFilters()

    End Sub


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        QueryRxnFileString = skInfo.MDLRxnFileString
        PerformQuery(skInfo.MDLRxnFileString)

    End Sub


    Private Sub btnSearch_Click() Handles btnSearch.Click

        PerformQuery(QueryRxnFileString)

    End Sub


    Private Sub PerformQuery(rxnFileStr As String)

        'testing only
        Dim queryFilters = New RSSQueryFilters With {
            .UserID = "seqTest",
            .ProjectName = "Project 1",
            .ProjectGroupName = "",
            .MinYield = Double.MinValue,
            .MaxYield = Double.MaxValue,
            .MinScale = Double.MinValue,
            .MaxScale = Double.MaxValue,
            .MinDate = Date.MinValue,
            .MaxDate = Date.MaxValue
        }

        ' perform filtered RSS query
        Dim newRssQuery As New Search(SearchContext, IsServerQuery)
        Dim hitExp = newRssQuery.FilteredRssQuery(rxnFileStr, queryFilters)

        If Not hitExp.Any Then
            lstRssHitGroups.DataContext = Nothing
            Dim finalizedStr = If(Not rdoServer.IsChecked, " - Only FINALIZED server experiments are listed.", "")
            cbMsgBox.Display("No matching experiments found." + finalizedStr, MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "No hits found")
            Exit Sub
        End If

        'the multi-property grouping criterion is implemented by concatenating the reactant and product InChIKeys 
        Dim sameRxnGroups = hitExp.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
                                 .Select(Function(group) New With {
                                     .MaxYield = group.Max(Function(item) item.Yield),
                                     .ExpEntries = group.OrderByDescending(Function(exp) exp.Yield)
                                 }).OrderByDescending(Function(group) group.MaxYield)

        'set global component structure color for obtained results
        SketchResults.ComponentStructureColor = Brushes.Black

        Dim rssGroups As New List(Of RssRxnGroup)
        Dim index As Integer = 1

        For Each grp In sameRxnGroups
            Dim firstExp = grp.ExpEntries.First
            Dim rssRxnGroup As New RssRxnGroup
            With rssRxnGroup
                Dim cbDrawInfo = DrawingEditor.GetSketchInfo(firstExp.RxnSketch)
                .GroupTitle = "Reaction " + index.ToString
                .ReactCanvas = cbDrawInfo.Reactants.First.StructureCanvas
                .ProdCanvas = cbDrawInfo.Products.First.StructureCanvas
                .MaxYield = grp.MaxYield
                .ExpItems = grp.ExpEntries
            End With
            rssGroups.Add(rssRxnGroup)
            index += 1
        Next


        '  Me.UpdateLayout()

        blkResultsTitle.Visibility = Visibility.Visible
        lstRssHitGroups.Visibility = Visibility.Visible

        lstRssHitGroups.DataContext = rssGroups

    End Sub


    Private Sub rdoLocal_CheckedChanged() Handles rdoLocal.Checked, rdoLocal.Unchecked

        If rdoLocal.IsInitialized Then

            SearchContext = If(rdoLocal.IsChecked, LocalDBContext, ServerDBContext)
            RefreshFilters()

            IsServerQuery = Not rdoLocal.IsChecked

            If QueryRxnFileString <> "" Then
                PerformQuery(QueryRxnFileString)
            End If

        End If

    End Sub


    Private Sub RefreshFilters()

        _cvsUsers.Source = SearchContext.tblUsers _
            .Select(Function(p) p.UserID) _
            .OrderBy(Function(t) t) _ 'orders the list elements (strings) in ascending order directly
            .ToList()

        _cvsProjects.Source = SearchContext.tblProjects _
            .Select(Function(p) p.Title) _
            .ToList() _
            .Distinct(StringComparer.OrdinalIgnoreCase) _
            .OrderBy(Function(t) t) _
            .ToList()

    End Sub



    ''' <summary>
    ''' Allows the use of the MouseWheel for results scrolling
    ''' </summary>
    ''' 
    Private Sub lstHitGroups_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles lstRssHitGroups.PreviewMouseWheel

        e.Handled = True

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) With {
            .RoutedEvent = ListBox.MouseWheelEvent,
            .Source = e.Source
        }

        lstRssHitGroups.RaiseEvent(e2)

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/ReactionSearches.html")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
            Me.Hide()
        End If

    End Sub

End Class


Public Class RssRxnGroup

    Public Property GroupTitle As String
    Public Property ReactCanvas As Canvas
    Public Property ProdCanvas As Canvas
    Public Property MaxYield As Double?
    Public Property ExpItems As IOrderedEnumerable(Of tblExperiments)

End Class


