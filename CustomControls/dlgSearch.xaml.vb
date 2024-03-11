Imports System.Windows.Controls
Imports System.Windows.Input
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

        '   Me.Left = Owner.Left + Owner.ActualWidth - Me.ActualWidth - 10
        Me.Left = Owner.Left + Owner.ActualWidth / 2 - Me.ActualWidth / 2
        Me.Top = Owner.Top + 30
        Me.MinHeight = 260
        Me.MaxHeight = 260

        CurrentDbContext = LocalDBContext

        If ServerDBContext Is Nothing Then
            rdoServer.IsEnabled = False
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
        Dim expHits = rxnSub.PerformRssQuery(rxnFileStr, CurrentDbContext)

        'the multi-property grouping criterion is achieved by concatenating the reactant and product InChIKeys 
        Dim sameRxnGroups = expHits.GroupBy(Function(item) item.ReactantInChIKey + "/" + item.ProductInChIKey) _
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

        Me.Left = Owner.Left + Owner.ActualWidth - Me.ActualWidth - 5
        Me.MaxHeight = Owner.ActualHeight - 20

        Me.UpdateLayout()

        blkResultsTitle.Visibility = Windows.Visibility.Visible
        lstRssHitGroups.Visibility = Windows.Visibility.Visible
        pnlNoHits.Visibility = If(rssGroups.Count > 0, Windows.Visibility.Collapsed, Windows.Visibility.Visible)

        lstRssHitGroups.DataContext = rssGroups

    End Sub


    Private Sub rdoLocal_CheckedChanged() Handles rdoLocal.Checked, rdoLocal.Unchecked

        CurrentDbContext = If(rdoLocal.IsChecked, LocalDBContext, ServerDBContext)

        IsServerQuery = Not rdoLocal.IsChecked

        If QueryRxnFileString <> "" Then
            PerformQuery(QueryRxnFileString)
        End If

    End Sub


    ''' <summary>
    ''' Allows the use of the MouseWheel for results scrolling
    ''' </summary>
    ''' 
    Private Sub lstProtocol_PreviewMouseWheel(sender As Object, e As MouseWheelEventArgs) Handles lstRssHitGroups.PreviewMouseWheel

        e.Handled = True

        Dim e2 As New MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        e2.RoutedEvent = ListBox.MouseWheelEvent
        e2.Source = e.Source

        lstRssHitGroups.RaiseEvent(e2)

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


