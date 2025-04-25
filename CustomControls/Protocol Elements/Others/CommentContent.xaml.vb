Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Media


Public Class CommentContent


    Private Property rtbSpellChecker As SpellChecker.SpellCheckerRtb


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        rtbSpellChecker = SpellChecker.CreateRichTextBoxSpellChecker(rtbComments)

    End Sub


    ''' <summary>
    ''' Defines the IsSpellCheckAllowed dependency property
    ''' </summary>
    '''
    Public Shared ReadOnly IsSpellCheckAllowedProperty As DependencyProperty = DependencyProperty.Register(
        "IsSpellCheckAllowed",
        GetType(Boolean),
        GetType(CommentContent),
        New PropertyMetadata(True, AddressOf OnIsSpellCheckAllowedChanged)
    )

    Public Property IsSpellCheckAllowed As Boolean

        Get
            Return CBool(GetValue(IsSpellCheckAllowedProperty))
        End Get
        Set(value As Boolean)
            SetValue(IsSpellCheckAllowedProperty, value)
        End Set

    End Property

    Private Shared Sub OnIsSpellCheckAllowedChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)

        Dim commentItem = TryCast(d, CommentContent)
        If commentItem IsNot Nothing Then
            Dim isAllowed = CBool(e.NewValue)
            If commentItem.rtbSpellChecker IsNot Nothing Then
                commentItem.rtbSpellChecker.IsSpellCheckAllowed = isAllowed
            End If
        End If

    End Sub


    ''' <summary>
    ''' The maximum allowed characters for user input. The default value 
    ''' of zero means no restriction.
    ''' </summary>
    ''' 
    Private Const MaxCharacters As Integer = 5000


    ''' <summary>
    ''' Brings the commentItem into active edit mode.
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
    ''' Disables all menu items that require text selection.
    ''' </summary>
    ''' 
    Private Sub mnuContext_Opened(sender As Object, e As RoutedEventArgs) Handles mnuContext.Opened

        ' handle spell check menu item

        Dim mousePos = Mouse.GetPosition(rtbComments)
        Dim suggestedWords = rtbSpellChecker.GetSuggestedWords(mousePos)

        If suggestedWords.Count = 0 Then

            mnuSpellCheck.Visibility = Visibility.Collapsed
            sepSpellCheck.Visibility = Visibility.Collapsed

        Else

            mnuSpellCheck.Visibility = Visibility.Visible
            sepSpellCheck.Visibility = Visibility.Visible

            With mnuSpellCheck

                .Items.Clear()

                If suggestedWords.Count > 0 Then
                    For Each suggestion In suggestedWords
                        Dim suggestItem = New MenuItem() With {.Header = suggestion}
                        AddHandler suggestItem.Click, AddressOf SuggestItem_Click
                        .Items.Add(suggestItem)
                    Next
                Else
                    .Items.Add(New MenuItem() With {.Header = "No suggestions available", .IsEnabled = False})
                End If

                .Items.Add(New Separator())

                Dim mnuAddToDictionary = New MenuItem() With {.Header = "Add to dictionary", .Tag = "AddDictionary"}
                AddHandler mnuAddToDictionary.Click, AddressOf mnuAddToDictionary_Click
                .Items.Add(mnuAddToDictionary)

                .IsSubmenuOpen = True

            End With

        End If

        'handle text selection menu items

        Dim selectedText As TextRange = rtbComments.Selection

        If selectedText.IsEmpty Then
            mnuBold.IsEnabled = False
            mnuItalic.IsEnabled = False
            mnuUnderline.IsEnabled = False
            mnuMarkerGroup.IsEnabled = False
            mnuClear.IsEnabled = False
        Else
            mnuBold.IsEnabled = True
            mnuItalic.IsEnabled = True
            mnuUnderline.IsEnabled = True
            mnuMarkerGroup.IsEnabled = True
            If HasUniformBackground(selectedText) AndAlso selectedText.GetPropertyValue(TextElement.BackgroundProperty) Is Nothing Then
                mnuClear.IsEnabled = False  'no highlight present in selection
            Else
                mnuClear.IsEnabled = True
            End If
        End If

    End Sub


    ''' <summary>
    ''' Handles spell check suggested word clicks.
    ''' </summary>
    '''
    Private Sub SuggestItem_Click(sender As Object, e As RoutedEventArgs)

        Dim clickedItem = TryCast(sender, MenuItem)
        If clickedItem IsNot Nothing Then
            rtbSpellChecker.ReplaceWordOccurrences(clickedItem.Header)
        End If

    End Sub


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


    Friend Sub HighlightBlue_Click(sender As Object, e As RoutedEventArgs)
        ApplyHighlight(TryFindResource("HighlightBlueBrush"))
        mnuContext.IsOpen = False
    End Sub

    Friend Sub HighlightGreen_Click(sender As Object, e As RoutedEventArgs)
        ApplyHighlight(TryFindResource("HighlightGreenBrush"))
        mnuContext.IsOpen = False
    End Sub

    Friend Sub HighlightPink_Click(sender As Object, e As RoutedEventArgs)
        ApplyHighlight(TryFindResource("HighlightPinkBrush"))
        mnuContext.IsOpen = False
    End Sub

    Friend Sub HighlightsClear_Click(sender As Object, e As RoutedEventArgs)
        ApplyHighlight(Nothing)
        mnuContext.IsOpen = False
    End Sub

    Private Sub mnuAddToDictionary_Click(sender As Object, e As RoutedEventArgs)

        Dim clickedItem = TryCast(sender, MenuItem)
        If clickedItem IsNot Nothing Then
            rtbSpellChecker.AddCurrentToDictionary()
        End If

    End Sub


    ''' <summary>
    ''' Toggles the specified background highlightBrush to the selected text to highlight it. If only part of the 
    ''' selection has the specified background, then the complete selection obtains is. If the selection 
    ''' is a subset of a larger range of the specified background, then its background is removed.
    ''' </summary>
    ''' 
    Private Sub ApplyHighlight(highlightBrush As Brush)

        Dim selectedText As TextRange = rtbComments.Selection

        If Not selectedText.IsEmpty Then

            If HasUniformBackground(selectedText) Then

                Dim currentBackground As Object = selectedText.GetPropertyValue(TextElement.BackgroundProperty)

                If TypeOf currentBackground Is Brush AndAlso currentBackground Is highlightBrush Then
                    ' Remove background if it matches the selected highlightBrush
                    selectedText.ApplyPropertyValue(TextElement.BackgroundProperty, Nothing)
                Else
                    ' Apply the new background highlightBrush
                    selectedText.ApplyPropertyValue(TextElement.BackgroundProperty, highlightBrush)
                End If
            Else

                ' Apply the new background highlightBrush
                selectedText.ApplyPropertyValue(TextElement.BackgroundProperty, highlightBrush)

            End If

            ' Move the caret to the end of the selection to remove the selection
            rtbComments.CaretPosition = selectedText.End

        End If

    End Sub


    ''' <summary>
    ''' Gets if the specified text selection has the same background highlightBrush. 
    ''' </summary>
    ''' 
    Private Function HasUniformBackground(selection As TextRange) As Boolean

        If selection.IsEmpty Then
            Return False
        End If

        Dim selStart As TextPointer = selection.Start
        Dim selEnd As TextPointer = selection.End
        Dim firstBackground As Brush = Nothing

        While selStart IsNot Nothing AndAlso selStart.CompareTo(selEnd) < 0

            Dim textRange As New TextRange(selStart, selStart.GetPositionAtOffset(1, LogicalDirection.Forward))
            Dim background As Brush = TryCast(textRange.GetPropertyValue(TextElement.BackgroundProperty), Brush)

            If firstBackground Is Nothing Then
                firstBackground = background
            ElseIf Not Equals(firstBackground, background) Then
                Return False
            End If

            selStart = selStart.GetNextContextPosition(LogicalDirection.Forward)

        End While

        Return True

    End Function


End Class
