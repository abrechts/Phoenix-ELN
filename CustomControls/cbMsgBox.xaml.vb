Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media

Public Class cbMsgBox

    Public Enum MessageType
        Information
        Warning
        [Error]
        Question
    End Enum

    Private _result As MsgBoxResult


    ''' <summary>
    ''' Drop-in replacement for VB.NET MsgBox with identical signature and behavior.
    ''' Simply replace MsgBox with cbMsgBox.Display() in your code - no parameters need to change.
    ''' </summary>
    ''' <param name="Prompt">The message to display</param>
    ''' <param name="Buttons">Button style and icon (use MsgBoxStyle enum values)</param>
    ''' <param name="Title">The window title</param>
    ''' <remarks>
    ''' Note that this implementation does not support the rarely used AbortRetryIgnore and RetryCancel button styles.
    ''' </remarks>
    ''' <returns>The result of the dialog interaction</returns>
    ''' 
    Public Shared Function Display(MessageStr As String,
                                    Optional [Buttons] As MsgBoxStyle = MsgBoxStyle.OkOnly,
                                    Optional [Title] As String = "") As MsgBoxResult

        ' Parse the MsgBoxStyle parameter
        Dim buttonType = ParseButtonType(Buttons)
        Dim messageType = ParseMessageType(Buttons)
        Dim defaultButton = ParseDefaultButton(Buttons, buttonType)

        ' Set default title if empty
        If String.IsNullOrEmpty(Title) Then
            Title = "Message"
        End If

        ' Remove single line breaks from the prompt text to handle legacy MsgBox string formatting
        MessageStr = NormalizeText(MessageStr)

        ' Create and show the dialog
        Dim msgBox = New cbMsgBox(MessageStr, Title, messageType, buttonType, defaultButton)

        If Application.Current?.MainWindow IsNot Nothing Then
            msgBox.Owner = Application.Current.MainWindow
        End If

        msgBox.ShowDialog()
        Return msgBox.GetResult()

    End Function


    ''' <summary>
    ''' Instance constructor (private - use the shared cbMsgBox function instead)
    ''' </summary>
    ''' 
    Private Sub New(message As String, Optional title As String = "Message",
                    Optional messageType As MessageType = MessageType.Information,
                    Optional buttons As Integer = 0,
                    Optional defaultButton As MsgBoxResult = MsgBoxResult.Ok)

        InitializeComponent()

        Me.Title = title
        MessageText.Text = message
        TitleText.Text = title

        ' Set icon and styling based on message type
        Select Case messageType

            Case MessageType.Warning
                IconText.Text = "⚠"
                IconText.Margin = New Thickness(0, -6, 0, 0)
                IconBorder.Background = New SolidColorBrush(Color.FromRgb(255, 159, 0))

            Case MessageType.Error
                IconText.Text = "✕"
                IconText.Margin = New Thickness(0, -2, 0, 0)
                IconBorder.Background = New SolidColorBrush(Color.FromRgb(222, 55, 10))

            Case MessageType.Question
                IconText.Text = "?"
                IconText.Margin = New Thickness(0, -3, 0, 0)
                IconBorder.Background = New SolidColorBrush(Color.FromRgb(0, 120, 212))

            Case Else ' Information
                IconText.Text = "ℹ"
                IconText.Margin = New Thickness(1, -3, 0, 0)
                IconBorder.Background = New SolidColorBrush(Color.FromRgb(0, 120, 212))

        End Select

        ' Configure buttons based on button type
        ConfigureButtons(buttons, defaultButton)

    End Sub


    Private Sub ConfigureButtons(buttonType As Integer, defaultButton As MsgBoxResult)

        ' Hide all buttons first
        btnButton1.Visibility = Visibility.Collapsed
        btnButton2.Visibility = Visibility.Collapsed
        btnButton3.Visibility = Visibility.Collapsed

        Select Case buttonType
            Case 0 ' OK only
                btnButton1.Content = "OK"
                btnButton1.Tag = MsgBoxResult.Ok
                btnButton1.Visibility = Visibility.Visible

            Case 1 ' OK, Cancel
                btnButton1.Content = "OK"
                btnButton1.Tag = MsgBoxResult.Ok
                btnButton1.Visibility = Visibility.Visible
                btnButton2.Content = "Cancel"
                btnButton2.Tag = MsgBoxResult.Cancel
                btnButton2.Visibility = Visibility.Visible

            Case 4 ' Yes, No
                btnButton1.Content = "Yes"
                btnButton1.Tag = MsgBoxResult.Yes
                btnButton1.Visibility = Visibility.Visible
                btnButton2.Content = "No"
                btnButton2.Tag = MsgBoxResult.No
                btnButton2.Visibility = Visibility.Visible

            Case 3 ' Yes, No, Cancel
                btnButton1.Content = "Yes"
                btnButton1.Tag = MsgBoxResult.Yes
                btnButton1.Visibility = Visibility.Visible
                btnButton2.Content = "No"
                btnButton2.Tag = MsgBoxResult.No
                btnButton2.Visibility = Visibility.Visible
                btnButton3.Content = "Cancel"
                btnButton3.Tag = MsgBoxResult.Cancel
                btnButton3.Visibility = Visibility.Visible

            Case Else ' Default to OK
                btnButton1.Content = "OK"
                btnButton1.Tag = MsgBoxResult.Ok
                btnButton1.Visibility = Visibility.Visible
        End Select

        ' Set the default button
        SetDefaultButton(defaultButton)

        ' Wire up click events
        AddHandler btnButton1.Click, AddressOf Button_Click
        AddHandler btnButton2.Click, AddressOf Button_Click
        AddHandler btnButton3.Click, AddressOf Button_Click

    End Sub


    '''' <summary>
    ''' Replaces single CRLFs with spaces (if not preceded by space) or removes them otherwise.
    ''' Preserves double CRLFs as paragraph breaks.
    ''' </summary>  
    ''' 
    Private Shared Function NormalizeText(origText As String) As String

        Dim result As String = Regex.Replace(origText, "(?<!\r\n)\r\n(?!\r\n)",
                  New MatchEvaluator(
                      Function(m As Match)
                          Dim idx = m.Index
                          If idx > 0 AndAlso origText(idx - 1) = " "c Then
                              Return ""      'preceded by space → remove
                          Else
                              Return " "     'not preceded by space → replace with space
                          End If
                      End Function))
        Return result

    End Function


    ''' <summary>
    ''' Handles ESC key to close dialog without action (returns Cancel if available, otherwise closes)
    ''' </summary>
    ''' 
    Private Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            _result = DetermineEscapeResult()
            Me.DialogResult = True
            Me.Close()
            e.Handled = True
        End If

    End Sub


    ''' <summary>
    ''' Determines appropriate result when ESC is pressed
    ''' </summary>
    Private Function DetermineEscapeResult() As MsgBoxResult

        ' If Cancel button is visible, return Cancel
        If btnButton2.Visibility = Visibility.Visible AndAlso CType(btnButton2.Tag, MsgBoxResult) = MsgBoxResult.Cancel Then
            Return MsgBoxResult.Cancel
        End If

        ' If No button is visible (Yes/No dialogs), return No
        If btnButton2.Visibility = Visibility.Visible AndAlso CType(btnButton2.Tag, MsgBoxResult) = MsgBoxResult.No Then
            Return MsgBoxResult.No
        End If

        ' If Cancel button is third button (Yes/No/Cancel), return Cancel
        If btnButton3.Visibility = Visibility.Visible AndAlso CType(btnButton3.Tag, MsgBoxResult) = MsgBoxResult.Cancel Then
            Return MsgBoxResult.Cancel
        End If

        ' Default to current result
        Return _result

    End Function



    ''' <summary>
    ''' Extracts the button type from MsgBoxStyle (bits 0-3)
    ''' Maps to cbMsgBox button types: 0=OK, 1=OK/Cancel, 4=Yes/No, 3=Yes/No/Cancel
    ''' </summary>
    Private Shared Function ParseButtonType(buttons As MsgBoxStyle) As Integer

        Dim buttonBits = CInt(buttons) And &HF ' Extract lower 4 bits

        Select Case buttonBits
            Case 0 ' OkOnly
                Return 0
            Case 1 ' OkCancel
                Return 1
            Case 3 ' YesNoCancel
                Return 3
            Case 4 ' YesNo
                Return 4
            Case Else ' Default to OK
                Return 0
        End Select

    End Function

    ''' <summary>
    ''' Extracts the message type/icon from MsgBoxStyle (bits 4-7)
    ''' </summary>
    Private Shared Function ParseMessageType(buttons As MsgBoxStyle) As MessageType

        Dim iconBits = CInt(buttons) And &HF0 ' Extract bits 4-7

        Select Case iconBits
            Case 16 ' Critical
                Return MessageType.Error
            Case 32 ' Question
                Return MessageType.Question
            Case 48 ' Exclamation
                Return MessageType.Warning
            Case 64 ' Information
                Return MessageType.Information
            Case Else ' Default to Information
                Return MessageType.Information
        End Select

    End Function


    ''' <summary>
    ''' Extracts the default button from MsgBoxStyle (bits 8-9)
    ''' DefaultButton1 = 0, DefaultButton2 = 256 (&H100), DefaultButton3 = 512 (&H200)
    ''' </summary>
    Private Shared Function ParseDefaultButton(buttons As MsgBoxStyle, buttonType As Integer) As MsgBoxResult

        Dim defaultBits = CInt(buttons) And &H300 ' Extract bits 8-9

        Select Case buttonType
            Case 0 ' OK only
                Return MsgBoxResult.Ok

            Case 1 ' OK/Cancel
                Select Case defaultBits
                    Case 0
                        Return MsgBoxResult.Ok
                    Case 256
                        Return MsgBoxResult.Cancel
                    Case Else
                        Return MsgBoxResult.Ok
                End Select

            Case 4 ' Yes/No 
                Select Case defaultBits
                    Case 0 '
                        Return MsgBoxResult.Yes
                    Case 256
                        Return MsgBoxResult.No
                    Case Else
                        Return MsgBoxResult.Yes
                End Select

            Case 3 ' Yes/No/Cancel
                Select Case defaultBits
                    Case 0
                        Return MsgBoxResult.Yes
                    Case 256
                        Return MsgBoxResult.No
                    Case 512
                        Return MsgBoxResult.Cancel
                    Case Else
                        Return MsgBoxResult.Yes
                End Select

            Case Else
                Return MsgBoxResult.Ok
        End Select

    End Function


    Private Sub SetDefaultButton(defaultButton As MsgBoxResult)

        ' Clear all IsDefault flags and reset styling
        btnButton1.IsDefault = False
        btnButton2.IsDefault = False
        btnButton3.IsDefault = False

        ' Set the appropriate button as default based on its tag
        If btnButton1.Visibility = Visibility.Visible AndAlso CType(btnButton1.Tag, MsgBoxResult) = defaultButton Then
            btnButton1.IsDefault = True
        ElseIf btnButton2.Visibility = Visibility.Visible AndAlso CType(btnButton2.Tag, MsgBoxResult) = defaultButton Then
            btnButton2.IsDefault = True
        ElseIf btnButton3.Visibility = Visibility.Visible AndAlso CType(btnButton3.Tag, MsgBoxResult) = defaultButton Then
            btnButton3.IsDefault = True
        End If

    End Sub

    Private Sub Button_Click(sender As Object, e As RoutedEventArgs)

        Dim button = CType(sender, Button)
        _result = CType(button.Tag, MsgBoxResult)
        Me.DialogResult = True
        Me.Close()

    End Sub

    Public Function GetResult() As MsgBoxResult

        Return _result

    End Function

End Class