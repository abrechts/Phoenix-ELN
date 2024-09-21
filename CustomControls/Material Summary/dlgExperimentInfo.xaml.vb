
Imports System.Windows.Controls
Imports System.Windows.Input

Public Class dlgExperimentInfo

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        tvReactantSummary.ItemsSource = MaterialsSummary.GetRefReactantGroups(Me.DataContext)
        tvReagentsSummary.ItemsSource = MaterialsSummary.GetReagentGroups(Me.DataContext)
        tvSolventsSummary.ItemsSource = MaterialsSummary.GetSolventGroups(Me.DataContext)
        tvAuxiliariesSummary.ItemsSource = MaterialsSummary.GetAuxiliariesGroups(Me.DataContext)

    End Sub


    Private Sub Me_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs) Handles Me.PreviewMouseDown

        UnselectAll(tvReactantSummary)
        UnselectAll(tvReagentsSummary)
        UnselectAll(tvSolventsSummary)
        UnselectAll(tvAuxiliariesSummary)

    End Sub


    ''' <summary>
    ''' Unselects all TreeViewItems of the specified TreeView
    ''' </summary>

    Private Shared Sub UnselectAll(treeView As TreeView)

        If Not treeView.IsFocused Then
            For Each item As Object In treeView.Items
                UnselectAllItems(CType(treeView.ItemContainerGenerator.ContainerFromItem(item), TreeViewItem))
            Next
        End If

    End Sub

    Private Shared Sub UnselectAllItems(treeViewItem As TreeViewItem)
        If treeViewItem Is Nothing Then
            Return
        End If

        treeViewItem.IsSelected = False

        For Each subItem As Object In treeViewItem.Items
            UnselectAllItems(CType(treeViewItem.ItemContainerGenerator.ContainerFromItem(subItem), TreeViewItem))
        Next

    End Sub

End Class


