Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data
Imports ElnBase
Imports ElnBase.ELNCalculations
Imports ElnBase.ELNEnumerations

Public Class WeightUnitConverter

    Implements IMultiValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        'Note: For a molar solution of a reagent, the Grams field contains the milliliters of the solution.

        If value(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim conv As String = parameter
        conv = conv.ToLower

        Dim grams As Double = value(0)
        Dim density As Double? = value(1)
        Dim displayAsVol As Boolean = value(2)
        Dim isMolar As Boolean = value(3)   'molar reagent solution

        Dim calcAsWeight = Not (displayAsVol Xor isMolar)
        If density Is Nothing AndAlso Not (calcAsWeight Xor isMolar) Then
            Return Nothing
        End If

        Dim scaled As ScaleResult

        If calcAsWeight Then
            grams = If(isMolar, grams * density, grams)
            scaled = ELNCalculations.ScaleWeight(grams)
        Else
            Dim milliliters = If(Not isMolar, grams / density, grams)
            scaled = ELNCalculations.ScaleVolume(milliliters)
        End If

        If conv = "combined" Then

            'returns amount and unit string
            Return SignificantDigitsString(scaled.Amount, SignificantDigits) + " " + scaled.Unit

        Else

            'returns either amount or unit string
            If conv = "amount" Then
                Return SignificantDigitsString(scaled.Amount, SignificantDigits)
            Else
                Return scaled.Unit
            End If

        End If

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function
End Class


Public Class VolumeUnitConverter

    Implements IMultiValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If value(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim conv As String = parameter
        conv = conv.ToLower

        Dim milliliters As Double = value(0)
        Dim density As Double? = value(1)
        Dim displayAsWeight As Boolean = value(2)

        Dim scaled As ScaleResult

        If Not displayAsWeight Then
            scaled = ScaleVolume(milliliters)
        Else
            Dim grams = milliliters * density
            scaled = ScaleWeight(grams)
        End If

        If conv = "combined" Then
            'returns amount and unit string
            Return SignificantDigitsString(scaled.Amount, SignificantDigits) + " " + scaled.Unit
        Else
            'returns either amount or unit string
            If conv = "amount" Then
                Return SignificantDigitsString(scaled.Amount, SignificantDigits)
            Else
                Return scaled.Unit
            End If
        End If

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function
End Class


Public Class MMolUnitConverter

    Implements IValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim mMols As Double = value
        Dim conv As String = parameter
        conv = conv.ToLower

        Dim scaled = ScaleMMol(mMols)

        If conv = "combined" Then
            'returns amount and unit string
            Return SignificantDigitsString(scaled.Amount, SignificantDigits) + " " + scaled.Unit
        Else
            'returns either amount or unit string
            If conv = "amount" Then
                Return SignificantDigitsString(scaled.Amount, SignificantDigits)
            Else
                Return scaled.Unit
            End If
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function

End Class


Public Class EquivUnitConverter

    Implements IValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim equiv As Double = value '* 1.00001  'to prevent rounding errors
        Dim conv As String = parameter
        conv = conv.ToLower

        Dim isShortUnit = (conv = "shortunit" OrElse conv = "combinedshort")
        Dim scaled = ELNCalculations.ScaleEquivalent(equiv, isshortunit)

        Select Case conv
            Case "amount", ""
                Return SignificantDigitsString(scaled.Amount, SignificantDigits)
            Case "unit", "shortunit"
                Return scaled.Unit
            Case Else
                'combined or combinedShort
                Return SignificantDigitsString(scaled.Amount, SignificantDigits) + " " + scaled.Unit
        End Select

    End Function


    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function

End Class


Public Class MatPropertyValConverter

    Implements IValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim itemVal As String = value

        If Val(itemVal) = 0 Then
            Return ""
        End If

        Return SignificantDigitsString(itemVal, SignificantDigits)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        If Val(value) = 0 Then
            Return Nothing
        Else
            Return value
        End If

    End Function

End Class


Public Class MolweightConverter

    Implements IValueConverter

    Public Shared Property SignificantDigits As Integer = 4

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim molweight As Double = value

        If Val(molweight) = 0 Then
            Return ""
        End If

        Return Format(molweight, "0.00")

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        If Val(value) = 0 Then
            Return Nothing
        Else
            Return value
        End If

    End Function

End Class


Public Class SolventEquivalentsConverter

    Implements IMultiValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If value(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim conv As String = parameter

        Dim equivalents As Double = value(0)
        Dim isMolEquiv As Boolean = value(1)
        Dim isShortNotation As Boolean = value(2)

        If Not isMolEquiv Then
            If conv = "amount" Then
                Return SignificantDigitsString(equivalents, SignificantDigits)
            Else
                Return If(isShortNotation, EquivUnitShort.vq.ToString, EquivUnit.volEquiv.ToString)
            End If
        Else
            If conv = "amount" Then
                Return SignificantDigitsString(equivalents, SignificantDigits)
            Else
                Return If(isShortNotation, EquivUnitShort.mv.ToString, EquivUnit.molEquiv.ToString)
            End If
        End If

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function

End Class


Public Class YieldConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim yield As Double = value

        If yield > 10 Then
            Return SignificantDigitsString(yield, 3) + "%"
        Else
            Return SignificantDigitsString(yield, 2) + "%" 'yields < 1%: diminished sigDigs
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
