

Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Media
Imports SpellCheck
Imports SpellCheck.Dictionaries


''' <summary>
''' Implements spell checkers for WPF RichTextBox and TextBox in various spell check languages, and supports 
''' ignored words. The global spell check element SpellChecker synchronizes all language changes and 
''' word exclusions across all RichTextBoxes and TextBoxes implementing it across the application. 
''' </summary>
''' <Remarks>Based on the basic opensource spellchecker https://github.com/ChrisPrusik/SpellCheck and implemented 
''' using the corresponding NuGet package.
''' </Remarks>
'''
Public Class SpellChecker

    Private Shared _customWordsFilePath As String = ""
    Private Shared _isEnabled As Boolean = True

    Friend Shared Event RequestUpdateSpellCheck()


    ''' <summary>
    ''' Gets a list of the available locales for spell checking
    ''' </summary>
    '''
    Public Shared ReadOnly Property AvailableLocales() As New List(Of String) From {"de-DE", "en-GB", "en-US", "es-ES", "fr-FR", "it-IT", "pt-PT"}


    ''' <summary>
    ''' Initializes the spell checker with the specified locale and the path to file containing the words to be ignored. 
    ''' If the specified locale is empty or not supported, the locale is set to the DefaultLocale value and the 
    ''' spell checker is set disabled.
    ''' </summary>
    ''' 
    Public Shared Sub Initialize(locale As String, ignoredWordsPath As String)

        Dim factory = New SpellCheckFactory()

        If AvailableLocales.Contains(locale) Then
            SpellCheckEngine = factory.CreateSpellChecker(locale).Result
        Else
            'spell check is off or invalid -> use en-US as default
            SpellCheckEngine = factory.CreateSpellChecker("en-US").Result
            IsEnabled = False
        End If

        CustomWordsFilePath = ignoredWordsPath

    End Sub


    ''' <summary>
    ''' Gets the core spell checker based on required NuGet package SpellCheck https://github.com/ChrisPrusik/SpellCheck. 
    ''' If not 
    ''' </summary>
    ''' 
    Private Shared Property SpellCheckEngine() As SpellCheck.SpellChecker


    ''' <summary>
    ''' Creates a new spell checker infrastructure for the specified RichTextBox.
    ''' </summary>
    ''' 
    Public Shared Function CreateRichTextBoxSpellChecker(targetRtb As RichTextBox) As SpellCheckerRtb

        Return New SpellCheckerRtb(targetRtb)

    End Function


    ''' <summary>
    ''' Creates a new spell checker infrastructure for the specified TextBox.
    ''' </summary>
    ''' 
    Public Shared Function CreateTextBoxSpellChecker(targetTextBox As TextBox) As SpellCheckerTb

        Return New SpellCheckerTb(targetTextBox)

    End Function


    ''' <summary>
    ''' Changes the language of the spell checker, e.g. "en-US" or "de-DE".
    ''' </summary>
    ''' <param name="localeStr">A valid and available locale string as enumerated by AvailableLocales.</param>
    ''' 
    Public Shared Sub ChangeLocale(localeStr As String)

        If AvailableLocales.Contains(localeStr) Then

            _isEnabled = True  'use private field to suppress update event

            Dim factory = New SpellCheckFactory()
            SpellCheckEngine = factory.CreateSpellChecker(localeStr).Result
            SpellCheckEngine.IgnoredWords = CombinedIgnoredWords

            RaiseEvent RequestUpdateSpellCheck()

        End If

    End Sub


    ''' <summary>
    ''' Gets or sets if spell checking is globally enabled.
    ''' </summary>
    ''' 
    Public Shared Property IsEnabled As Boolean

        Get
            Return _isEnabled
        End Get

        Set(value As Boolean)
            If value <> _isEnabled Then
                _isEnabled = value
                RaiseEvent RequestUpdateSpellCheck()
            End If
        End Set

    End Property


    ''' <summary>
    ''' Sets the path to the ignored words file and reads its contents to populate CustomIgnoredWords from it.
    ''' </summary>
    ''' 
    Private Shared Property CustomWordsFilePath As String

        Get
            Return _customWordsFilePath
        End Get

        Set(value As String)

            ' try to create file if it doesn't exit yet
            If Not File.Exists(value) Then
                Try
                    Using File.Create(value)
                    End Using
                Catch ex As Exception
                    cbMsgBox.Display("Can't create custom dictionary file!", MsgBoxStyle.Information, "Spell Checker")
                    Exit Property
                End Try
            End If

            _customWordsFilePath = value

            Dim customUserWords = File.ReadAllLines(_customWordsFilePath).ToList
            customUserWords = customUserWords.Where(Function(word) Not String.IsNullOrWhiteSpace(word)).ToList()  'remove empty or whitespace-only lines

            'combine with internal ignore words (union deduplicates items)
            CombinedIgnoredWords.Clear()
            CombinedIgnoredWords = customUserWords.Union(InternalIgnoredWords).ToList

            SpellCheckEngine.IgnoredWords = CombinedIgnoredWords

        End Set

    End Property


    ''' <summary>
    ''' Gets the the internal list of words to be ignored, to be combined with the user list
    ''' </summary>
    '''
    Private Shared ReadOnly Property InternalIgnoredWords As New List(Of String) From
        {"jpg", "jpeg", "png", "pdf", "docx", "xlsx", "pptx",
        "rpm", "mbar", "bar", "torr", "Torr", "GC", "HPLC", "MS", "analytics", "Analytics",
        "chromatography", "dropwise", "recrystallization"}


    ''' <summary>
    ''' Gets a list of all current user defined ignore words.
    ''' </summary>
    ''' 
    Public Shared ReadOnly Property CustomIgnoredWords As List(Of String)
        Get
            Return CombinedIgnoredWords.Except(InternalIgnoredWords).ToList
        End Get
    End Property


    ''' <summary>
    ''' Sets or gets a combination of user specified custom and internal words to be ignored by the spell checker. 
    ''' </summary>
    ''' 
    Private Shared Property CombinedIgnoredWords As New List(Of String)


    ''' <summary>
    ''' Adds the specified word to the custom user dictionary and updates the current spell checks.
    ''' </summary>
    ''' 
    Public Shared Sub AddToCustomWords(ignoreWord As String)

        If CombinedIgnoredWords.Contains(ignoreWord) Then
            Exit Sub
        End If

        CombinedIgnoredWords.Add(ignoreWord)
        SpellCheckEngine.IgnoredWords = CombinedIgnoredWords
        RaiseEvent RequestUpdateSpellCheck()

        Try
            File.WriteAllLines(CustomWordsFilePath, CustomIgnoredWords)
        Catch ex As Exception
        End Try

    End Sub


    ''' <summary>
    ''' Removes the specified list of words from the custom user dictionary and updates the current spell checks.
    ''' </summary>
    ''' 
    Public Shared Sub RemoveFromCustomWords(delWords As List(Of String))

        If delWords IsNot Nothing Then

            CombinedIgnoredWords = CombinedIgnoredWords.Except(delWords).ToList
            SpellCheckEngine.IgnoredWords = CombinedIgnoredWords
            RaiseEvent RequestUpdateSpellCheck()

            Try
                File.WriteAllLines(CustomWordsFilePath, CombinedIgnoredWords.Except(InternalIgnoredWords))
            Catch ex As Exception
            End Try

        End If

    End Sub


    ''' <summary>
    ''' Gets a list of misspelled words in the sourceStr.
    ''' </summary>
    ''' 
    Public Shared Function GetSpellingErrors(sourceStr As String) As List(Of String)

        Dim errWords As New List(Of String)

        'remove email and url parts of the string
        sourceStr = Extensions.StringExtensions.RemoveEmails(sourceStr)
        sourceStr = Extensions.StringExtensions.RemoveUrls(sourceStr)

        Dim myWords = Extensions.StringExtensions.GetWords(sourceStr)

        For Each wd In myWords
            If Not SpellCheckEngine.IsWordCorrect(wd) Then
                errWords.Add(wd)
            End If
        Next

        Return errWords

    End Function


    ''' <summary>
    ''' Gets a list of suggested words for the specified misspelled word, filtering out identical words 
    ''' and ensuring similarity also for custom user words.
    ''' </summary>
    '''
    Public Shared Function SuggestWords(errWord As String) As List(Of String)

        ' Get lexicon suggestions
        Dim lexiconWordList = If(SpellCheckEngine.SuggestWord(errWord).ToList(), New List(Of String))

        ' Filter suggestions: similar but not identical
        Dim similarityThreshold As Integer = 2 ' Define maximum allowed edit distance
        Dim customList = (CombinedIgnoredWords.Where(Function(word) Not word.Equals(errWord) AndAlso
            LevenshteinDistance(errWord, word) <= similarityThreshold)).ToList

        'combine lexicon and custom suggestions (excludes duplicates)
        Return lexiconWordList.Union(customList).ToList

    End Function


    ''' <summary>
    ''' Gets a list of the positions of all full-word matching occurrences of a wordStr in a given srcString. Partial word matches are ignored.
    ''' </summary>
    ''' 
    Private Shared Function FindFullWordIndices(ByVal srcString As String, ByVal wordStr As String) As List(Of Integer)

        Dim indices As New List(Of Integer)()
        Dim pattern As String = "\b" & Regex.Escape(wordStr) & "\b" ' \b ensures full-word match

        Dim matches As MatchCollection = Regex.Matches(srcString, pattern)
        For Each match As Match In matches
            indices.Add(match.Index)
        Next

        Return indices

    End Function


    ''' <summary>
    ''' Spell checking: Calculates the Levenshtein Distance between two strings.
    ''' </summary>
    ''' <param name="source">The source string.</param>
    ''' <param name="target">The target string.</param>
    ''' <returns>The Levenshtein Distance.</returns>
    ''' 
    Private Shared Function LevenshteinDistance(source As String, target As String) As Integer

        'make this case-independent
        source = source.ToLower
        target = target.ToLower

        Dim n = source.Length
        Dim m = target.Length
        Dim dp(n + 1, m + 1) As Integer

        For i = 0 To n
            dp(i, 0) = i
        Next
        For j = 0 To m
            dp(0, j) = j
        Next

        For i = 1 To n
            For j = 1 To m
                Dim cost = If(source(i - 1) = target(j - 1), 0, 1)
                dp(i, j) = Math.Min(Math.Min(dp(i - 1, j) + 1, dp(i, j - 1) + 1), dp(i - 1, j - 1) + cost)
            Next
        Next

        Return dp(n, m)

    End Function


    ' -----------------------------------------------
    ' This is the RichTextBox specific spell checker
    ' -----------------------------------------------

    Public Class SpellCheckerRtb

        Private Property ErrorRanges As New List(Of TextRange)
        Private WithEvents _parentRtb As RichTextBox
        Private _lastSuggestionRange As TextRange
        Private _suppressUpdate As Boolean = False

        Friend Sub New(sourceRtb As RichTextBox)

            _parentRtb = sourceRtb
            AddHandler SpellChecker.RequestUpdateSpellCheck, AddressOf rtbSpellChecker_RequestUpdateSpellCheck

        End Sub

        Private Sub rtbSpellChecker_RequestUpdateSpellCheck()

            If _parentRtb.IsVisible Then
                DoSpellCheck()
            End If

        End Sub


        Private Sub parentRtb_Loaded() Handles _parentRtb.Loaded

            If _parentRtb.IsVisible Then
                DoSpellCheck()
            End If

        End Sub


        ''' <summary>
        ''' Sets or gets if spell checking is allowed on the parent RichTextBox.
        ''' </summary>
        '''
        Public Property IsSpellCheckAllowed As Boolean
            Get
                Return _isSpellCheckAllowed
            End Get
            Set(value As Boolean)
                _isSpellCheckAllowed = value
                DoSpellCheck()
            End Set
        End Property

        Private _isSpellCheckAllowed As Boolean = True


        ''' <summary>
        ''' Performs the actual spell checking and highlights the misspelled words. Actual spell checking only works in combination 
        ''' with SpellChecker.IsEnabled.
        ''' </summary>
        ''' 
        Public Sub DoSpellCheck()

            If Not _parentRtb.IsMeasureValid OrElse Not _parentRtb.IsLoaded Then
                Exit Sub
            End If

            ClearAllSpellingErrors()

            If SpellChecker.IsEnabled AndAlso IsSpellCheckAllowed Then

                Dim textRange As New TextRange(_parentRtb.Document.ContentStart, _parentRtb.Document.ContentEnd)
                Dim text As String = textRange.Text

                If text IsNot Nothing Then

                    Dim errors = SpellChecker.GetSpellingErrors(text)

                    If errors.Count > 0 Then
                        Dim adornLayer = AdornerLayer.GetAdornerLayer(_parentRtb)
                        HighlightAllWordOccurrences(errors, adornLayer)
                    End If

                End If

            End If

        End Sub


        Private Sub HighlightAllWordOccurrences(errorWords As List(Of String), adornLayer As AdornerLayer)

            Dim errorWordSet As New HashSet(Of String)(errorWords)
            Dim errRectangles As New List(Of Rect)
            ErrorRanges.Clear()

            Dim pointer = _parentRtb.Document.ContentStart

            While pointer IsNot Nothing AndAlso pointer.CompareTo(_parentRtb.Document.ContentEnd) < 0
                If pointer.GetPointerContext(LogicalDirection.Forward) = TextPointerContext.Text Then
                    Dim textRun As String = pointer.GetTextInRun(LogicalDirection.Forward)
                    Dim matches = Regex.Matches(textRun, "\b\w+\b")

                    For Each match As Match In matches
                        Dim word = match.Value
                        If errorWordSet.Contains(word) Then
                            Dim wordStartPtr = pointer.GetPositionAtOffset(match.Index)
                            Dim wordEndPtr = wordStartPtr.GetPositionAtOffset(word.Length)
                            Dim errWordRange As New TextRange(wordStartPtr, wordEndPtr)
                            ErrorRanges.Add(errWordRange)

                            Dim start = wordStartPtr

                            While start IsNot Nothing AndAlso start.CompareTo(wordEndPtr) < 0

                                ' Find the end of the current line, or the word end, whichever comes first
                                Dim lineEnd = start.GetLineStartPosition(1)
                                If lineEnd Is Nothing OrElse lineEnd.CompareTo(wordEndPtr) > 0 Then
                                    lineEnd = wordEndPtr
                                Else
                                    ' Move to the last character of the current line
                                    lineEnd = lineEnd.GetPositionAtOffset(0, LogicalDirection.Backward)
                                    If lineEnd Is Nothing OrElse lineEnd.CompareTo(start) <= 0 Then
                                        lineEnd = wordEndPtr
                                    End If
                                End If

                                ' Enumerate all rectangles for the word range (handles line wraps)
                                Dim rectStart = start.GetCharacterRect(LogicalDirection.Forward)
                                Dim rectEnd = lineEnd.GetCharacterRect(LogicalDirection.Backward)
                                If Not rectStart.IsEmpty AndAlso Not rectEnd.IsEmpty Then
                                    Dim rect As New Rect(rectStart.TopLeft, rectEnd.BottomRight)
                                    rect.Offset(0, -2)
                                    If rect.Width > 0 AndAlso rect.Height > 0 Then
                                        errRectangles.Add(rect)
                                    End If
                                End If

                                If lineEnd Is wordEndPtr Then
                                    Exit While
                                End If
                                start = lineEnd

                            End While
                        End If
                    Next
                End If
                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward)
            End While

            If errRectangles.Count > 0 Then
                Dim underlineAdorner = New ErrorLineAdorner(_parentRtb, errRectangles)
                adornLayer.Add(underlineAdorner)
            End If

        End Sub



        ''' <summary>
        ''' Replaces the current word in error by the specified suggestion.
        ''' </summary>
        ''' 
        Public Sub ReplaceWordOccurrences(replacement As String)

            If _lastSuggestionRange IsNot Nothing Then

                'clear errors before modification of text ranges; will be placed again by DoSpellCheck.
                ClearAllSpellingErrors()

                Dim errWord = _lastSuggestionRange.Text
                ReplaceAllWordOccurrences(errWord, replacement)

                DoSpellCheck()  'required since text changed event was suppressed in ReplaceAllWordOccurrence

            End If

        End Sub


        Private Sub ReplaceAllWordOccurrences(wordToReplace As String, replacementWord As String)

            Dim pointer As TextPointer = _parentRtb.Document.ContentStart

            _suppressUpdate = True
            _parentRtb.BeginChange()    'for block undo

            While pointer IsNot Nothing AndAlso pointer.CompareTo(_parentRtb.Document.ContentEnd) < 0

                If pointer.GetPointerContext(LogicalDirection.Forward) = TextPointerContext.Text Then

                    Dim textRun As String = pointer.GetTextInRun(LogicalDirection.Forward)

                    Dim regex As New Regex($"\b{Regex.Escape(wordToReplace)}\b", RegexOptions.None)
                    Dim matches = regex.Matches(textRun)

                    For i = matches.Count - 1 To 0 Step -1

                        Dim match = matches(i)
                        Dim startPointer = pointer.GetPositionAtOffset(match.Index)
                        Dim endPointer = startPointer.GetPositionAtOffset(match.Length)
                        Dim textRange As New TextRange(startPointer, endPointer)
                        ' Replace the text without altering formatting
                        If textRange.Text = wordToReplace Then
                            textRange.Text = replacementWord
                        End If
                    Next
                End If

                pointer = pointer.GetNextContextPosition(LogicalDirection.Forward)

            End While

            _parentRtb.EndChange()
            _suppressUpdate = False

        End Sub


        ''' <summary>
        ''' Gets a list of suggested words for the wordStr at the specified mouse position in RichTextBox coordinates.
        ''' </summary>
        '''
        Public Function GetSuggestedWords(mousePos As Point) As List(Of String)

            _lastSuggestionRange = GetErrorRangeUnderMouse(mousePos)

            If _lastSuggestionRange IsNot Nothing Then
                Dim suggestedWords = SpellChecker.SuggestWords(_lastSuggestionRange.Text)
                suggestedWords = (From word In suggestedWords Where Not word.Contains(" "c)).ToList()
                Return suggestedWords.ToList
            Else
                Return New List(Of String)
            End If

        End Function


        ''' <summary>
        ''' Adds the current word in error to the ignored words dictionary and saves the updated ignoredWords file.
        ''' </summary>
        '''
        Public Sub AddCurrentToDictionary()

            If _lastSuggestionRange IsNot Nothing Then

                SpellChecker.AddToCustomWords(_lastSuggestionRange.Text)
                ErrorRanges.Remove(_lastSuggestionRange)

            End If

        End Sub


        ''' <summary>
        ''' Clears all spell check errors.
        ''' </summary>
        ''' 
        Private Sub ClearAllSpellingErrors()

            Dim adornLayer = AdornerLayer.GetAdornerLayer(_parentRtb)
            If adornLayer IsNot Nothing Then
                Dim adorners = adornLayer.GetAdorners(_parentRtb)
                If adorners IsNot Nothing Then
                    For Each adorner In adorners
                        adornLayer.Remove(adorner)
                    Next
                End If
            End If

            ErrorRanges.Clear()

        End Sub


        Private Sub ProcessTextChanged(sender As Object, e As TextChangedEventArgs) Handles _parentRtb.TextChanged

            If SpellChecker.IsEnabled AndAlso Not _suppressUpdate Then
                If Not IsCaretAtEndOfWord() Then
                    DoSpellCheck()
                End If
            End If

        End Sub


        Private Sub ProcessKeyUp(sender As Object, e As KeyEventArgs) Handles _parentRtb.PreviewKeyUp

            If e.Key = Key.Delete OrElse e.Key = Key.Back Then
                DoSpellCheck()
            End If

            'take over undo/redo - required after replacing misspelled words
            If e.Key = Key.Z AndAlso Keyboard.Modifiers = ModifierKeys.Control Then
                _parentRtb.Undo()
                DoSpellCheck()

            ElseIf e.Key = Key.Y AndAlso Keyboard.Modifiers = ModifierKeys.Control Then
                _parentRtb.Redo()
                DoSpellCheck()

            End If

        End Sub


        Private Sub PreviewMouseLeftButtonUp() Handles _parentRtb.PreviewMouseLeftButtonUp

            If SpellChecker.IsEnabled Then
                If _parentRtb.IsKeyboardFocused Then
                    DoSpellCheck()
                End If
            End If

        End Sub


        Private Sub LostKeyBoardFocus() Handles _parentRtb.LostKeyboardFocus

            If SpellChecker.IsEnabled Then
                DoSpellCheck()
            End If

        End Sub



        '''' <summary>
        '''' Gets if current RichTextBox caret is located directly at the end of a word.
        '''' This typically indicates that the user is typing a new, so far incomplete word.
        '''' </summary>
        '''' 
        Private Function IsCaretAtEndOfWord() As Boolean

            Dim caretPointer As TextPointer = _parentRtb.CaretPosition

            ' Check if the caret is at the start of the document
            If caretPointer Is Nothing OrElse caretPointer.GetPointerContext(LogicalDirection.Backward) = TextPointerContext.None Then
                Return False
            End If

            Dim previousText As String = caretPointer.GetTextInRun(LogicalDirection.Backward)
            Dim isPrevCharWord As Boolean = Not String.IsNullOrEmpty(previousText) AndAlso Char.IsLetterOrDigit(previousText.Last())

            Dim nextText = caretPointer.GetTextInRun(LogicalDirection.Forward)
            Dim isNextCharNonWord = String.IsNullOrEmpty(nextText) OrElse Not Char.IsLetterOrDigit(nextText.First())

            Return isPrevCharWord AndAlso isNextCharNonWord

        End Function


        ''' <summary>
        ''' Gets the TextRange of the error wordStr under the mouse position, and returns nothing if no error is present
        ''' </summary>
        ''' 
        Private Function GetErrorRangeUnderMouse(mousePos As Point) As TextRange

            For Each errRange In ErrorRanges
                Dim targetPointer As TextPointer = _parentRtb.GetPositionFromPoint(mousePos, True)
                If targetPointer IsNot Nothing AndAlso errRange.Contains(targetPointer) Then
                    Return errRange
                End If
            Next

            Return Nothing

        End Function


    End Class


    ' ----------------------------------
    ' This is the TextBox spell checker
    ' ----------------------------------

    Public Class SpellCheckerTb

        ' Private Property ErrorRectangles As New List(Of Rect)
        Public Property SpellErrors As New List(Of SpellErrorInfo)

        Private WithEvents _parentTextBox As TextBox
        Private Shared _isEnabled As Boolean = True
        Private LastErrRef As SpellErrorInfo

        Private Shared _customWordsFilePath As String = ""


        Friend Sub New(sourceTextBox As TextBox)

            _parentTextBox = sourceTextBox
            AddHandler SpellChecker.RequestUpdateSpellCheck, AddressOf rtbSpellChecker_RequestUpdateSpellCheck

        End Sub


        Private Sub rtbSpellChecker_RequestUpdateSpellCheck()

            If _parentTextBox.IsVisible Then
                DoSpellCheck()
            End If

        End Sub


        Private Sub parentTextBox_Loaded() Handles _parentTextBox.Loaded

            If _parentTextBox.IsVisible Then
                DoSpellCheck()
            End If

        End Sub


        ''' <summary>
        ''' Sets or gets if spell checking is allowed on the parent RichTextBox.
        ''' </summary>
        '''
        Public Property IsSpellCheckAllowed As Boolean
            Get
                Return _isSpellCheckAllowed
            End Get
            Set(value As Boolean)
                _isSpellCheckAllowed = value
                DoSpellCheck()
            End Set
        End Property

        Private _isSpellCheckAllowed As Boolean = True



        ''' <summary>
        ''' Performs the actual spell checking and highlights the misspelled words.
        ''' </summary>
        ''' 
        Public Sub DoSpellCheck()

            If Not _parentTextBox.IsMeasureValid OrElse Not _parentTextBox.IsLoaded Then
                Exit Sub
            End If

            ClearAllSpellingErrors()

            If SpellChecker.IsEnabled AndAlso IsSpellCheckAllowed Then

                Dim text As String = _parentTextBox.Text

                If text IsNot Nothing Then
                    Dim errors = SpellChecker.GetSpellingErrors(text)
                    If errors IsNot Nothing AndAlso errors.Count > 0 Then
                        Dim adornLayer = AdornerLayer.GetAdornerLayer(_parentTextBox)
                        HighlightAllWordOccurrences(errors, adornLayer)
                    End If
                End If

            End If

        End Sub


        Private Sub HighlightAllWordOccurrences(errorWords As List(Of String), adornLayer As AdornerLayer)

            Dim errorWordSet As New HashSet(Of String)(errorWords)
            Dim errRectangles As New List(Of Rect)
            SpellErrors.Clear()

            Dim fullText = _parentTextBox.Text
            If String.IsNullOrEmpty(fullText) OrElse errorWordSet.Count = 0 Then Return

            ' Use regex to find all words and their positions
            Dim matches = Regex.Matches(fullText, "\b\w+\b")
            For Each match As Match In matches
                Dim word = match.Value
                If errorWordSet.Contains(word) Then
                    Try
                        Dim rect1 = _parentTextBox.GetRectFromCharacterIndex(match.Index)
                        Dim rect2 = _parentTextBox.GetRectFromCharacterIndex(match.Index + word.Length)
                        If Not rect1.IsEmpty AndAlso Not rect2.IsEmpty Then
                            rect1.Width = rect2.Right - rect1.Left
                            rect1.Offset(0, -2)
                            Dim errInfo As New SpellErrorInfo
                            With errInfo
                                .ErrorRectangle = rect1
                                .MisspelledWord = word
                                SpellErrors.Add(errInfo)
                            End With
                            errRectangles.Add(rect1)
                        End If
                    Catch ex As Exception
                    End Try
                End If
            Next

            If errRectangles.Count > 0 Then
                Dim underlineAdorner = New ErrorLineAdorner(_parentTextBox, errRectangles)
                adornLayer.Add(underlineAdorner)
            End If

        End Sub


        ''' <summary>
        ''' Gets a list of suggested words for the wordStr at the specified mouse position in RichTextBox coordinates.
        ''' </summary>
        '''
        Public Function GetSuggestedWords(mousePos As Point) As List(Of String)

            LastErrRef = GetErrorInfoUnderMouse(mousePos)

            If LastErrRef IsNot Nothing Then
                Dim suggestedWords = SpellChecker.SuggestWords(LastErrRef.MisspelledWord)
                suggestedWords = (From word In suggestedWords Where Not word.Contains(" "c)).ToList()
                Return suggestedWords.ToList
            Else
                Return New List(Of String)
            End If

        End Function

        ''' <summary>
        ''' Replaces all occurrences of the current word in error by the specified suggestion.
        ''' </summary>
        ''' 
        Public Sub ReplaceWordOccurrences(replacement As String)

            If LastErrRef IsNot Nothing Then

                Dim fullText = _parentTextBox.Text
                _parentTextBox.Text = fullText.Replace(LastErrRef.MisspelledWord, replacement)  'replaces all occurrences in fullText
                SpellErrors.Remove(LastErrRef)

            End If

        End Sub


        ''' <summary>
        ''' Adds the current word in error to the ignored words dictionary and saves the updated ignoredWords file.
        ''' </summary>
        '''
        Public Sub AddCurrentToDictionary()

            If LastErrRef IsNot Nothing Then

                SpellChecker.AddToCustomWords(LastErrRef.MisspelledWord)
                SpellErrors.Remove(LastErrRef)

            End If

        End Sub


        ''' <summary>
        ''' Clears all spell check errors.
        ''' </summary>
        ''' 
        Private Sub ClearAllSpellingErrors()

            Dim adornLayer = AdornerLayer.GetAdornerLayer(_parentTextBox)
            If adornLayer IsNot Nothing Then
                Dim adorners = adornLayer.GetAdorners(_parentTextBox)
                If adorners IsNot Nothing Then
                    For Each adorner In adorners
                        adornLayer.Remove(adorner)
                    Next
                End If
            End If

            SpellErrors.Clear()

        End Sub


        Private Sub ProcessTextChanged(sender As Object, e As TextChangedEventArgs) Handles _parentTextBox.TextChanged

            If SpellChecker.IsEnabled Then
                If Not IsCaretAtEndOfWord() Then
                    DoSpellCheck()
                End If
            End If

        End Sub


        Private Sub ProcessKeyUp(sender As Object, e As KeyEventArgs) Handles _parentTextBox.PreviewKeyUp

            If e.Key = Key.Z AndAlso Keyboard.Modifiers = ModifierKeys.Control Then
                _parentTextBox.Undo()
                DoSpellCheck()

            ElseIf e.Key = Key.Y AndAlso Keyboard.Modifiers = ModifierKeys.Control Then
                _parentTextBox.Redo()
                DoSpellCheck()

            End If

            If e.Key = Input.Key.Delete OrElse e.Key = Input.Key.Back Then
                DoSpellCheck()
            End If

        End Sub


        Private Sub PreviewMouseUp() Handles _parentTextBox.PreviewMouseUp

            If SpellChecker.IsEnabled Then
                If _parentTextBox.IsKeyboardFocused Then
                    DoSpellCheck()
                End If
            End If

        End Sub


        Private Sub LostKeyboardFocus() Handles _parentTextBox.LostKeyboardFocus

            If SpellChecker.IsEnabled Then
                DoSpellCheck()
            End If

        End Sub


        ''' <summary>
        ''' Gets if the TextBox caret is immediately at the end of a word.
        ''' </summary>
        ''' 
        Private Function IsCaretAtEndOfWord() As Boolean

            Dim caretPos = _parentTextBox.CaretIndex
            Dim text = _parentTextBox.Text

            If caretPos > 0 AndAlso caretPos <= text.Length Then

                Dim prevChar = text(caretPos - 1) ' Character before the caret
                Dim nextChar As Char? = If(caretPos < text.Length, text(caretPos), Nothing) ' Character at the caret position

                Dim isPrevCharWord = Char.IsLetterOrDigit(prevChar)
                Dim isNextCharNonWord = Not nextChar.HasValue OrElse Not Char.IsLetterOrDigit(nextChar.Value)

                Return isPrevCharWord AndAlso isNextCharNonWord
            End If

            Return False

        End Function


        ''' <summary>
        ''' Gets the TextRange of the error wordStr under the mouse position, and returns nothing if no error is present
        ''' </summary>
        ''' 
        Private Function GetErrorInfoUnderMouse(mousePos As Point) As SpellErrorInfo

            For Each errInfo In SpellErrors
                If errInfo.ErrorRectangle.Contains(mousePos) Then
                    Return errInfo
                End If
            Next

            Return Nothing

        End Function

        Public Class SpellErrorInfo

            Public Property ErrorRectangle As Rect
            Public Property MisspelledWord As String

        End Class

    End Class


    '------------------
    '-------------------

    Public Class ErrorLineAdorner

        Inherits Adorner

        Private ReadOnly _errorRanges As List(Of Rect)

        Public Sub New(adornedElement As UIElement, errorRanges As List(Of Rect))

            MyBase.New(adornedElement)
            _errorRanges = errorRanges

        End Sub


        Protected Overrides Sub OnRender(drawingContext As DrawingContext)

            MyBase.OnRender(drawingContext)

            Dim rectPen As New Pen(Brushes.Red, 0.5)
            For Each rect In _errorRanges
                ' drawingContext.DrawLine(rectPen, rect.BottomLeft, rect.BottomRight)
                DrawWigglyLine(drawingContext, rect.BottomLeft, rect.BottomRight, amplitude:=2, frequency:=5, rectPen)
            Next

        End Sub


        Private Shared Sub DrawWigglyLine(drawingContext As DrawingContext, startPoint As Point, endPoint As Point, amplitude As Double,
               frequency As Double, pen As Pen)

            Dim geometry As New StreamGeometry()

            Using ctx = geometry.Open()
                ' Start the wiggly line at the startPoint
                ctx.BeginFigure(startPoint, False, False)

                ' Calculate the total length of the line
                Dim totalLength = endPoint.X - startPoint.X

                ' Calculate the number of full waves and the remaining distance
                Dim waveCount = Math.Floor(totalLength / frequency)
                Dim remainingLength = totalLength - (waveCount * frequency)

                ' Generate the wavy pattern
                Dim currentX = startPoint.X
                Dim currentY = startPoint.Y
                For i = 0 To waveCount - 1
                    ' Draw the upward curve
                    ctx.QuadraticBezierTo(New Point(currentX + frequency / 4, currentY - amplitude),
                             New Point(currentX + frequency / 2, currentY), True, True)

                    ' Draw the downward curve
                    ctx.QuadraticBezierTo(New Point(currentX + 3 * frequency / 4, currentY + amplitude),
                            New Point(currentX + frequency, currentY), True, True)

                    ' Move to the next wave
                    currentX += frequency
                Next

                ' Handle the remaining distance
                If remainingLength > 0 Then
                    ' Draw a partial wave to reach the endPoint
                    Dim midPointX = currentX + remainingLength / 2
                    ctx.QuadraticBezierTo(New Point(currentX + remainingLength / 4, currentY - amplitude),
                             New Point(midPointX, currentY), True, True)
                    ctx.QuadraticBezierTo(New Point(currentX + 3 * remainingLength / 4, currentY + amplitude),
                             endPoint, True, True)
                End If

            End Using

            ' Freeze the geometry for performance
            geometry.Freeze()

            ' Draw the wiggly line
            drawingContext.DrawGeometry(Nothing, pen, geometry)
        End Sub

    End Class

End Class






