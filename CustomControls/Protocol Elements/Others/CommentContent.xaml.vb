Imports System.Windows
Imports System.Windows.Documents
Imports System.Windows.Input

Public Class CommentContent

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' The maximum allowed characters for user input. The default value 
    ''' of zero means no restriction.
    ''' </summary>
    ''' 
    Private Const MaxCharacters As Integer = 5000


    ''' <summary>
    ''' Brings the control into active edit mode.
    ''' </summary>
    ''' 
    Public Sub ActivateEdit()

        With rtbComments
            .Focus()
        End With

    End Sub


    Private Function GetTextLength() As Integer

        Dim tr = New TextRange(rtbComments.Document.ContentStart, rtbComments.Document.ContentEnd)
        Return tr.Text.Length

    End Function


    ''' <summary>
    ''' Validates the content to be pasted
    ''' </summary>
    ''' 
    Private Sub rtbComments_Pasting(sender As Object, e As DataObjectPastingEventArgs)

        'Efficiently restricts pasting to unformatted text only, i.e. no images etc. 
        e.FormatToApply = DataFormats.Text

        'limit total text length
        If Clipboard.ContainsText Then

            Dim clipText = Clipboard.GetText()
            If (GetTextLength() + clipText.Length) > MaxCharacters Then

                e.CancelCommand()

                Dispatcher.BeginInvoke(CType(Function() MsgBox("Pasting the clipboard content would exceed" + vbCrLf +
                  "the maximum allowed text length!", MsgBoxStyle.Information, "Comment Validation"), Action))

            End If
        End If

    End Sub


    Private Sub rtbComments_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles rtbComments.PreviewKeyDown

        If Keyboard.Modifiers = ModifierKeys.Control Then

            If e.Key = Key.L Then
                'bullet list item
                EditingCommands.ToggleBullets.Execute(Nothing, rtbComments)
                Exit Sub
            End If

        End If

        If (GetTextLength() >= MaxCharacters) Then

            e.Handled = True
            MsgBox("Maximum comment length reached!", MsgBoxStyle.Information, "Comment Validation")

        End If

    End Sub


    Private Sub rtbComments_LostFocus() Handles rtbComments.LostFocus

        With rtbComments
            If .CanUndo Then
                .IsUndoEnabled = False  'clears the internal undo stack
                .IsUndoEnabled = True   'restarts it again
                Dim myProtocol = WPFToolbox.FindVisualParent(Of Protocol)(Me)
                myProtocol?.AutoSave()
            End If
        End With

    End Sub


End Class
