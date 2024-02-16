Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnCoreModel

Public Class WorkflowSeparator

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    ''' <summary>
    ''' Brings the control into active edit mode.
    ''' </summary>
    ''' 
    Public Sub ActivateEdit()

        With txtTitle
            .Focus()
            .SelectAll()
        End With

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Return OrElse e.Key = Key.Enter Then

            'finalize edit by removing focus to parent list box item
            Dim myItem = WPFToolbox.FindVisualParent(Of ListBoxItem)(Me)
            myItem.Focus()

        End If

    End Sub


    Private Sub txtTitle_LostFocus() Handles txtTitle.LostFocus

        With txtTitle
            If .CanUndo Then
                .IsUndoEnabled = False  'clears the internal undo stack
                .IsUndoEnabled = True   'restarts it again
                Dim myProtocol = WPFToolbox.FindVisualParent(Of Protocol)(Me)
                myProtocol?.AutoSave()
            End If
        End With

    End Sub

End Class
