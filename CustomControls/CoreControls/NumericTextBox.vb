Imports System.Threading
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ElnBase

Public Class NumericTextBox

    Inherits TextBox

    Public Sub New()

        DataObject.AddPastingHandler(Me, AddressOf PasteHandler)

    End Sub


    Public Property PositiveNumbersOnly As Boolean = False

    Public Property IntegersOnly As Boolean = False

    Public Property DecimalCount As Integer = -1

    Public Property SignificantDigits As Integer = 0


    ''' <summary>
    ''' Sets or gets the entered string value as double.
    ''' </summary>
    ''' 
    Public Property Value As Double?

        Get
            Return StringToDouble(Me.Text)
        End Get

        Set(value As Double?)
            If value IsNot Nothing Then
                Me.Text = DoubleToString(value, DecimalCount, SignificantDigits)
            Else
                Me.Text = ""
            End If
        End Set

    End Property


    ''' <summary>
    ''' Gets if the value is zero or nothing.
    ''' </summary>
    ''' 
    Public Function IsZeroOrNothing() As Boolean

        Return (Value Is Nothing OrElse Value = 0)

    End Function


    Public Function IsValidNumericText(inputStr As String) As Boolean

        Dim res As IEnumerable(Of Char)

        If IntegersOnly Then
            If inputStr.Contains("."c) OrElse inputStr.Contains(","c) Then
                Return False
            End If
        End If

        If PositiveNumbersOnly Then
            res = From myChar In inputStr Where Not (Char.IsDigit(myChar) OrElse myChar = "." _
            OrElse myChar = ",")
        Else
            res = From myChar In inputStr Where Not (Char.IsDigit(myChar) OrElse myChar = "-" OrElse myChar = "." _
            OrElse myChar = ",")
        End If

        Return Not res.Any

    End Function


    ''' <summary>
    ''' Prevent non-numeric text input
    ''' </summary>
    ''' 
    Private Sub Me_PreviewTextInput(sender As Object, e As TextCompositionEventArgs) Handles Me.PreviewTextInput

        Dim currText = e.OriginalSource.Text

        'prevent double decimal separator
        If (e.Text.Contains("."c) OrElse e.Text.Contains(","c)) AndAlso (currText.Contains(".") OrElse currText.Contains(",")) Then
            e.Handled = True
            Exit Sub
        End If

        'prevent negative sign at any place other than at string start
        If Not PositiveNumbersOnly AndAlso (e.Text.Contains("-"c) AndAlso e.OriginalSource.caretindex > 0) Then
            e.Handled = True
            Exit Sub
        End If

        'check if otherwise valid number string
        If Not IsValidNumericText(e.Text) Then
            e.Handled = True
        End If

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        'TextInput does not fire on SpaceBar ...
        If e.Key = Key.Space Then
            e.Handled = True
        End If

    End Sub


    Private Sub PasteHandler(sender As Object, e As DataObjectPastingEventArgs)

        Dim textOK = False

        If (e.DataObject.GetDataPresent(GetType(String))) Then
            Dim pasteText As String = e.DataObject.GetData(GetType(String))
            If IsNumeric(pasteText) Then
                textOK = True
            End If
        End If

        If Not textOK Then
            e.CancelCommand()
            CType(sender, TextBox).Text = ""    'deletes all text in case of an error
        End If

    End Sub

    ''' <summary>
    ''' Converts a number string into a double floating number, tolerating both comma and period decimal separators.
    ''' </summary>
    ''' <param name="numberStr">Number string to convert</param>
    ''' <returns>Number as double, or nothing if numberStr can't be converted to a double.</returns>
    ''' <remarks></remarks>
    ''' 
    Public Shared Function StringToDouble(ByVal numberStr As String) As Double?

        Dim currDecSeparator As String = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator
        Dim currNumGroupingSep As String = Thread.CurrentThread.CurrentCulture.NumberFormat.NumberGroupSeparator

        If numberStr = "" OrElse numberStr = "-" OrElse numberStr = "." OrElse numberStr = "," Then
            Return Nothing
        Else

            'swap decimal separators if non-locale one is used 
            If currDecSeparator = "." Then
                numberStr = numberStr.Replace(",", ".")
            Else
                numberStr = numberStr.Replace(".", ",")
            End If
            numberStr = numberStr.Replace(" ", "")   'remove blanks
            Try
                Return Double.Parse(numberStr)
            Catch ex As Exception
                Return Nothing
            End Try

        End If

    End Function


    ''' <summary>
    ''' Converts a double value to a string utilizing the currently set decimal separator.
    ''' </summary>
    ''' <param name="val">The double value to convert to string.</param>
    ''' <param name="decCount">The number of digits after the decimal to create. A negative value  
    ''' indicates not to perform any decimal digits formatting (original digits kept).</param>
    ''' <returns>A string representing the passed single value with correct decimal separator and 
    ''' specified digits after the decimal (if required)</returns>
    ''' 
    Public Shared Function DoubleToString(val As Double, decCount As Integer, sigDigCount As Integer) As String

        Try
            If sigDigCount > 0 Then
                Return ELNCalculations.SignificantDigitsString(val, sigDigCount)
            ElseIf decCount >= 0 Then
                Return Format(val, "F" + decCount.ToString)   'the F format does not create group separators (like N)
            Else
                Return Format(val)
            End If
        Catch ex As Exception
            Return ""
        End Try

    End Function


End Class

