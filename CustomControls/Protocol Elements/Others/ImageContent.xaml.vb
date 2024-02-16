Imports System.Windows.Controls
Imports ElnCoreModel

Public Class ImageContent

    Public Shared Event MouseOverChanged(sender As Object, isEntering As Boolean)

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub ActivateEdit()

        txtComments.Focus()
        txtComments.Select(255, 0)

    End Sub


    Private Sub displayImg_MouseEnter() Handles displayImg.MouseEnter
        RaiseEvent MouseOverChanged(Me, True)
    End Sub


    Private Sub displayImg_MouseLeave() Handles displayImg.MouseLeave
        RaiseEvent MouseOverChanged(Me, False)
    End Sub


    Private Sub blkRotate_MouseUp() Handles icoRotate.MouseUp

        Dim fileEntry = CType(Me.DataContext, tblEmbeddedFiles)

        If fileEntry.IsRotated = 0 Then
            fileEntry.IsRotated = 1
        Else
            fileEntry.IsRotated = 0
        End If

        WPFToolbox.FindVisualParent(Of Protocol)(Me).AutoSave()

    End Sub


    Private Sub displayImg_MouseLeftButtonUp() Handles displayImg.MouseLeftButtonUp

        Dim fullImageDlg As New dlgFullImage
        With fullImageDlg
            .DataContext = Me.DataContext
            .ShowDialog()
        End With

    End Sub


    Private Sub txtComments_GotKeyboardFocus() Handles txtComments.GotKeyboardFocus

        txtComments.Select(255, 0)

    End Sub


    Private Sub txtComments_LostFocus() Handles Me.LostFocus

        With txtComments
            If .CanUndo Then
                .IsUndoEnabled = False  'clears the undo stack
                .IsUndoEnabled = True   'restarts it again
                Dim myProtocol = WPFToolbox.FindVisualParent(Of Protocol)(Me)
                myProtocol?.AutoSave()
            End If
        End With

    End Sub


End Class
