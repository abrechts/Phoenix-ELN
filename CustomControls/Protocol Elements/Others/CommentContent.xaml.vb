Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Markup
Imports System.Windows.Media
Imports Clipboard = System.Windows.Clipboard


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
    ''' The maximum allowed number of lines of comment text. This prevents print page break issues and also 
    ''' ensures a balanced visual protocol layout.
    ''' </summary>
    ''' 
    Private Const MaxLines As Integer = 25


    ''' <summary>
    ''' Brings the commentItem into active edit mode.
    ''' </summary>
    ''' 
    Public Sub ActivateEdit()

        With rtbComments
            .Focus()
        End With

    End Sub


    ''' <summary>
    ''' Gets the current number of characters the XAML representation of the comment document contains.
    ''' </summary>
    ''' 
    Private Function GetDocLength() As Integer

        Dim docXAML = XamlWriter.Save(rtbComments.Document)
        Return docXAML.Length

    End Function


    ''' <summary>
    ''' Performs spell checking after pasting
    ''' </summary>
    '''
    Private Sub PasteExecuted(sender As Object, e As ExecutedRoutedEventArgs)   'XAML event

        rtbSpellChecker.DoSpellCheck()

    End Sub



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
            mnuSuperscript.IsEnabled = False
            mnuSubscript.IsEnabled = False
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
            Dim isDigitsOnly = Trim(selectedText.Text).All(AddressOf Char.IsDigit)    'digits only selected?
            mnuSuperscript.IsEnabled = isDigitsOnly
            mnuSubscript.IsEnabled = isDigitsOnly
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

        If Clipboard.ContainsData(DataFormats.Rtf) Then

            ' Insert RTF content
            '--------------------

            'detect embedded rtf graphics
            Dim rtfText As String = Clipboard.GetData(DataFormats.Rtf) '.ToString()
            Dim pictRegex As New Regex("\\pict", RegexOptions.IgnoreCase)

            If pictRegex.IsMatch(rtfText) Then

                ' cancel if embedded images in RTF
                Dispatcher.BeginInvoke(CType(Function() MsgBox("Sorry, can't paste comments " + vbCrLf +
                        "containing graphics!", MsgBoxStyle.Information, "Comment Validation"), Action))
                e.CancelCommand()
                Exit Sub

            End If

            ' normalize content and insert into rtbComments (instead of paste)
            InsertRTF(rtfText, rtbComments, MaxLines)
            e.CancelCommand()


        ElseIf Clipboard.ContainsData(DataFormats.Text) OrElse Clipboard.ContainsData(DataFormats.UnicodeText) Then

            ' Insert plain or unicode text content
            '-------------------------------------

            Dim isUnicodeText = Clipboard.ContainsData(DataFormats.UnicodeText)

            'check for max size limit
            Dim clipText = If(isUnicodeText, Clipboard.GetData(DataFormats.UnicodeText), Clipboard.GetData(DataFormats.Text))

            ' cancel if resulting in too many lines
            If TooManyResultingLines(rtbComments, clipText, MaxLines) Then
                Dispatcher.BeginInvoke(CType(Function() MsgBox("Sorry, pasting would exceed the " + vbCrLf +
                    $"limit of {MaxLines} comment lines!", MsgBoxStyle.Information, "Comment Validation"), Action))
                e.CancelCommand()
                Exit Sub
            End If

            With rtbComments

                .BeginChange()

                Dim caretRange = New TextRange(.CaretPosition, .CaretPosition)
                With caretRange
                    .Text = clipText
                    ' apply format changes in FlowDocument here ....
                    .ApplyPropertyValue(TextElement.FontSizeProperty, rtbComments.FontSize)
                    .ApplyPropertyValue(TextElement.FontFamilyProperty, rtbComments.FontFamily)
                    .ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black)  'required if formatted text of different foreground was pasted just before.
                End With

                ' apply default block line height
                For Each block In .Document.Blocks
                    If TypeOf block Is Paragraph Then
                        block.Margin = New Thickness(0)
                    End If
                Next

                rtbComments.CaretPosition = caretRange.End

                .EndChange()

            End With

            e.FormatToApply = If(isUnicodeText, DataFormats.UnicodeText, DataFormats.Text)
            e.CancelCommand()

        Else

            'skip any other format
            e.CancelCommand()

        End If

    End Sub


    ''' <summary>
    ''' Converts the specified RTF string into a FlowDocument, normalizes it to fit the required font and line spacing 
    ''' properties, and finally inserts the result into into targetRtb at its current caret position.
    ''' </summary>
    ''' <returns>False, if the resulting number of text lines would exceed the specified maxLines.</returns>
    ''' 
    Private Function InsertRTF(rtfText As String, targetRtb As RichTextBox, maxLines As Integer) As Boolean

        Dim tmpRtb As New RichTextBox

        ' load RTF into the temp document
        Dim tmpRange = New TextRange(tmpRtb.Document.ContentStart, tmpRtb.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward))
        Using ms As New MemoryStream(Encoding.UTF8.GetBytes(rtfText))
            tmpRange.Load(ms, DataFormats.Rtf)
        End Using

        Dim doc = tmpRtb.Document

        ' apply format changes in FlowDocument here ....
        tmpRange.ApplyPropertyValue(TextElement.FontSizeProperty, rtbComments.FontSize)
        tmpRange.ApplyPropertyValue(TextElement.FontFamilyProperty, rtbComments.FontFamily)

        ' remove trailing empty newline, if present 
        If doc.Blocks.Count > 0 Then
            Dim lastBlock = TryCast(doc.Blocks.LastBlock, Paragraph)
            If lastBlock IsNot Nothing AndAlso String.IsNullOrEmpty(New TextRange(lastBlock.ContentStart, lastBlock.ContentEnd).Text.Trim()) Then
                doc.Blocks.Remove(lastBlock)
                tmpRange = New TextRange(tmpRtb.Document.ContentStart, tmpRtb.Document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward))
            End If
        End If

        ' cancel if table elements present
        If doc.Blocks.OfType(Of Table).Any Then
            Dispatcher.BeginInvoke(CType(Function() MsgBox("Sorry, can't paste table elements " + vbCrLf +
               "(including from HTML content).", MsgBoxStyle.Information, "Comment Validation"), Action))
            Return False
        End If

        ' cancel if resulting in too many rtbComments text lines
        If TooManyResultingLines(rtbComments, tmpRange.Text, maxLines) Then
            Dispatcher.BeginInvoke(CType(Function() MsgBox("Sorry, pasting would exceed the " + vbCrLf +
                  $"limit of {maxLines} comment lines!", MsgBoxStyle.Information, "Comment Validation"), Action))
            Return False
        End If

        ' apply default block line height
        For Each block In doc.Blocks
            If TypeOf block Is Paragraph Then
                block.Margin = New Thickness(0)
            End If
        Next

        ' apply default bullet list line height
        For Each list As List In doc.Blocks.OfType(Of List)()
            For Each listItem As ListItem In list.ListItems
                For Each para As Paragraph In listItem.Blocks.OfType(Of Paragraph)()
                    para.Margin = New Thickness(0)
                Next
            Next
        Next

        With targetRtb

            .BeginChange()

            Dim dstRange = New TextRange(.CaretPosition, .CaretPosition)
            Using ms As New MemoryStream
                tmpRange.Save(ms, DataFormats.XamlPackage)
                ms.Seek(0, SeekOrigin.Begin)
                dstRange.Load(ms, DataFormats.XamlPackage)
            End Using

            .CaretPosition = dstRange.End

            ForceBlackTyping(rtbComments)

            .EndChange()

        End With

        Return True

    End Function


    ''' <summary>
    ''' Ensures that typing at the text caret position occurs in default font properties, including foreground color.
    ''' </summary>
    ''' 
    Private Sub ForceBlackTyping(rtb As RichTextBox)

        ' If already black, nothing to do
        Dim current = rtb.Selection.GetPropertyValue(TextElement.ForegroundProperty)
        If TypeOf current Is SolidColorBrush AndAlso DirectCast(current, SolidColorBrush).Color = Colors.Black Then
            Exit Sub
        End If

        ' Locate the inline and its paragraph
        Dim caret = rtb.CaretPosition
        Dim inline = GetAncestorInline(caret)
        Dim para As Paragraph = caret.Paragraph

        If para Is Nothing AndAlso inline IsNot Nothing Then
            para = TryCast(inline.Parent, Paragraph)
        ElseIf para Is Nothing Then
            para = New Paragraph()
            rtb.Document.Blocks.Add(para)
        End If

        ' Create and insert the black Run
        Dim blackRun = New Run With {
            .Foreground = Brushes.Black,
            .FontSize = rtbComments.FontSize,
            .FontFamily = rtbComments.FontFamily
        }

        If inline IsNot Nothing Then
            para.Inlines.InsertAfter(inline, blackRun)
        Else
            para.Inlines.Add(blackRun)
        End If

        ' Move caret into the new run
        rtb.CaretPosition = blackRun.ContentEnd

    End Sub


    Private Function GetAncestorInline(pointer As TextPointer) As Inline

        Dim d As DependencyObject = pointer.Parent
        While d IsNot Nothing AndAlso TypeOf d IsNot Inline
            If TypeOf d Is TextElement Then
                d = DirectCast(d, TextElement).Parent
            Else
                Exit While
            End If
        End While

        Return TryCast(d, Inline)

    End Function


    Private _wasKeyPressed As Boolean = False

    Private Sub rtbComments_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles rtbComments.PreviewKeyDown

        _wasKeyPressed = True

        If Keyboard.Modifiers = ModifierKeys.Control Then

            If e.Key = Key.L Then

                ' bullet list item
                EditingCommands.ToggleBullets.Execute(Nothing, rtbComments)

            ElseIf e.Key = Key.Z OrElse e.Key = Key.Y Then

                ' important for preventing double undo or redo!
                e.Handled = True

            End If

        Else

            ' cancel entering new line exceeding MaxLines
            If e.Key = Key.Return OrElse e.Key = Key.Enter Then

                If GetVisualLineCount(rtbComments) >= MaxLines Then
                    MsgBox("Sorry, adding another line would exceed" + vbCrLf +
                        $"the limit of {MaxLines} comment lines.", MsgBoxStyle.Information, "Comment Validation")
                    e.Handled = True
                End If

            End If

        End If

    End Sub


    ''' <summary>
    ''' Catches rtb height changes caused by new lines appearing after word wrapping
    ''' </summary>
    ''' 
    Private Sub rtbComments_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles rtbComments.SizeChanged

        If _wasKeyPressed AndAlso e.NewSize.Height > e.PreviousSize.Height Then

            If GetVisualLineCount(rtbComments) > MaxLines Then

                'remove previously entered character
                Dim prevPos As TextPointer = rtbComments.CaretPosition.GetPositionAtOffset(-1, LogicalDirection.Forward)
                If prevPos IsNot Nothing Then
                    Dim tr As New TextRange(prevPos, rtbComments.CaretPosition)
                    tr.Text = String.Empty
                End If

                MsgBox($"Sorry, the maximum number of {MaxLines} comment " + vbCrLf +
                        "lines would be exceed when inserting this " + vbCrLf +
                       "character, due to word wrapping.", MsgBoxStyle.Information, "Comment Validation")

            End If

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


    ''' <summary>
    ''' Gets if the maxNumber of text lines in the RichTextBox would be exceeded if the specified insertStr would be inserted.
    ''' </summary>
    ''' 
    Private Function TooManyResultingLines(rtb As RichTextBox, insertStr As String, maxLines As Integer) As Boolean

        ' MaxLines+1 is applied for compensating the line count overlap always resulting from inserting
        ' into an existing original line (no matter if empty or not).

        Return GetVisualLineCount(rtbComments) + GetInsertTextVisualLineCount(rtbComments, insertStr) > (maxLines + 1)

    End Function


    Private Function GetInsertTextVisualLineCount(targetRtb As RichTextBox, insertStr As String) As Integer

        ' Create a temporary RichTextBox to measure the visual lines

        Dim tempRtb As New RichTextBox()

        With tempRtb

            .FontSize = targetRtb.FontSize
            .FontFamily = targetRtb.FontFamily
            .Width = targetRtb.ActualWidth
            .Padding = targetRtb.Padding

            .Document.Blocks.Clear()
            .Document.Blocks.Add(New Paragraph(New Run(insertStr)))

            .Measure(New Size)
            .Arrange(New Rect)

            Return GetVisualLineCount(tempRtb)

        End With

    End Function



    ''' <summary>
    ''' Gets the number of lines actually present in the FlowDocument of the specified RichTextBox, including wrapped text.
    ''' </summary>
    ''' 
    Private Function GetVisualLineCount(rtb As RichTextBox) As Integer

        If Not rtb.IsLoaded Then
            rtb.UpdateLayout()
        End If

        Dim pointer As TextPointer = rtb.Document.ContentStart.GetLineStartPosition(0)
        Dim lastY As Double = -1
        Dim count As Integer = 0

        While pointer IsNot Nothing AndAlso pointer.CompareTo(rtb.Document.ContentEnd) < 0

            Dim rect = pointer.GetCharacterRect(LogicalDirection.Forward)
            If rect.Top > lastY Then
                count += 1
                lastY = rect.Top
            End If

            pointer = pointer.GetLineStartPosition(1)

        End While

        Return count

    End Function


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
