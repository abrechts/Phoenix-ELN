Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows
Imports ElnBase
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


    Private Sub Me_Loaded() Handles Me.Loaded

        CurrentDbContext = LocalDBContext

        If ServerDBContext Is Nothing Then
            IsServerQuery = False
            rdoServer.IsEnabled = False
        Else
            rdoServer.IsChecked = IsServerQuery
        End If

    End Sub


    Public Property LocalDBContext As ElnDbContext

    Public Property ServerDBContext As ElnDbContext

    Private Property CurrentDbContext As ElnDbContext   'assigned rdoLocal and rdoServer setting

    Private Property QueryRxnFileString As String


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        QueryRxnFileString = skInfo.MDLRxnFileString
        PerformQuery(skInfo.MDLRxnFileString)

    End Sub


    Private Sub PerformQuery(rxnFileStr As String)

        Dim rxnSub As New RxnSubstructure
        Dim expHitInfo = rxnSub.PerformRssQuery(rxnFileStr, CurrentDbContext, IsServerQuery)

        Select Case expHitInfo.ErrorType

            Case RssErrorType.None
                'do nothing and proceed

            Case RssErrorType.TooManyHits
                lstRssHitGroups.DataContext = Nothing
                MsgBox("Too many hits expected - please" + vbCrLf +
                       "make your query more specific!", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "RSS query error")
                Exit Sub

            Case RssErrorType.QueryStructureError
                lstRssHitGroups.DataContext = Nothing
                MsgBox("There's a structure error in your" + vbCrLf +
                       "query sketch (stereochemistry?)." + vbCrLf +
                       "Please correct!", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "RSS query error")
                Exit Sub

        End Select

        'the multi-property grouping criterion is achieved by concatenating the reactant and product InChIKeys 
        Dim sameRxnGroups = expHitInfo.ExperimentHits.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
                             .Select(Function(group) New With {
                                 .MaxYield = group.Max(Function(item) item.Yield),
                                 .ExpEntries = group.OrderByDescending(Function(exp) exp.Yield)
                             }).OrderByDescending(Function(group) group.MaxYield)

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

        Me.Top = Owner.Top + 5
        Me.Left = Owner.Left + Owner.ActualWidth - Me.ActualWidth - 5
        Me.MaxHeight = Owner.ActualHeight - 20
        Me.Height = Me.MaxHeight

        Me.UpdateLayout()

        blkResultsTitle.Visibility = Visibility.Visible
        lstRssHitGroups.Visibility = Visibility.Visible
        pnlNoHits.Visibility = If(rssGroups.Count > 0, Visibility.Collapsed, Visibility.Visible)

        lstRssHitGroups.DataContext = rssGroups

    End Sub


    Private Sub rdoLocal_CheckedChanged() Handles rdoLocal.Checked, rdoLocal.Unchecked

        If rdoLocal.IsInitialized Then

            CurrentDbContext = If(rdoLocal.IsChecked, LocalDBContext, ServerDBContext)

            IsServerQuery = Not rdoLocal.IsChecked

            blkFinalizedInfo.Visibility = If(IsServerQuery, Visibility.Visible, Visibility.Collapsed)

            If QueryRxnFileString <> "" Then
                PerformQuery(QueryRxnFileString)
            End If

        End If

    End Sub


    ''' <summary>
    ''' Allows the use of the MouseWheel for results scrolling
    ''' </summary>
    ''' 
    Private Sub lstHitGroups_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles lstRssHitGroups.PreviewMouseWheel

        e.Handled = True

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        e2.RoutedEvent = ListBox.MouseWheelEvent
        e2.Source = e.Source

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


