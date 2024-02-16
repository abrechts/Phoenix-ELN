

Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Partial Public Class ExpTreeHeader

    Public Shared Event RequestUpdateWorkflowState(sender As Object, targetExp As tblExperiments, requestedState As WorkflowStatus)

    Public Sub New()

        ' This call is required by the Windows Form Designer.
        InitializeComponent()

    End Sub


    Private Sub Me_PreviewMouseRightButtonDown(sender As Object, e As RoutedEventArgs) Handles Me.PreviewMouseRightButtonUp

        Dim tvItem = WPFToolbox.FindVisualParent(Of TreeViewItem)(Me)
        If tvItem IsNot Nothing Then
            tvItem.IsSelected = True
        End If

    End Sub


    Private Sub mnuFinalize_Click() Handles mnuFinalize.Click

        Dim expEntry = CType(DataContext, tblExperiments)
        RaiseEvent RequestUpdateWorkflowState(Me, expEntry, WorkflowStatus.Finalized)

    End Sub


    Private Sub mnuUnlock_Click() Handles mnuUnlock.Click

        Dim expEntry = CType(DataContext, tblExperiments)
        RaiseEvent RequestUpdateWorkflowState(Me, expEntry, WorkflowStatus.Unlocked)

    End Sub

End Class


Public Class ExpStateToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim workflowState As WorkflowStatus = value

        If LCase(parameter) = "invert" Then
            Return If(workflowState <> WorkflowStatus.Finalized, Visibility.Visible, Visibility.Collapsed)
        Else
            Return If(workflowState = WorkflowStatus.Finalized, Visibility.Visible, Visibility.Collapsed)
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class


Public Class PinnedToVisibilityConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim displayIndex As Integer = value

        Select Case displayIndex
            Case -1, > 0
                Return Visibility.Visible
            Case Else
                Return Visibility.Hidden
        End Select

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
