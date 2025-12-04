Imports System.ComponentModel
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnBase.ELNCalculations


''' <summary>
''' Sorts navigation tree projects in descending SequenceNr order. This converter type generally provides otherwise 
''' unavailable support for sorting Enums within HierarchicalDataTemplates.
''' </summary>
''' 
Public Class ProjectsCollectionViewConverter

    Implements IValueConverter

    Public Property View As ListCollectionView

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        View = New ListCollectionView(CType(value, IList))
        View.SortDescriptions.Add(New SortDescription("SequenceNr", ListSortDirection.Descending))
        Return View

    End Function

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim view = CType(value, CollectionView)
        Return view.SourceCollection

    End Function

End Class


Public Class ProjectFoldersCollectionViewConverter

    Implements IValueConverter

    Public Property View As ListCollectionView

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        View = New ListCollectionView(CType(value, IList))
        View.SortDescriptions.Add(New SortDescription("SequenceNr", ListSortDirection.Descending))
        Return View

    End Function

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim view = CType(value, CollectionView)
        Return view.SourceCollection

    End Function

End Class



''' <summary>
''' Sorts navigation tree project experiments in descending order. This converter type generally provides otherwise 
''' unavailable support for sorting Enums within HierarchicalDataTemplates.
''' </summary>
''' 
Public Class ExperimentsCollectionViewConverter

    Implements IValueConverter

    Public Property View As ListCollectionView

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        View = New ListCollectionView(CType(value, IList))
        View.SortDescriptions.Add(New SortDescription("ExperimentID", ListSortDirection.Descending))
        Return View

    End Function

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim view = CType(value, CollectionView)
        Return view.SourceCollection

    End Function

End Class


Public Class StatusIconConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.Convert

        Dim imgSrc As Viewbox

        Select Case CType(value, WorkflowStatus)
            Case WorkflowStatus.Finalized
                imgSrc = Application.Current.FindResource("icoOkBulletBlue")'
            Case WorkflowStatus.InProgress
                imgSrc = Application.Current.FindResource("icoRedDot")'
            Case WorkflowStatus.Unlocked
                imgSrc = Application.Current.FindResource("icoReopened") '
            Case Else
                imgSrc = Application.Current.FindResource("icoReopened") '
        End Select

        Return imgSrc

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.ConvertBack
        Return Nothing
    End Function

End Class


Public Class TabStatusIconConverter

    Implements IMultiValueConverter

    Public Function Convert(value() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object _
    Implements IMultiValueConverter.Convert

        Dim workFlowState As WorkflowStatus
        Dim displayIndex As Integer

        Try
            workFlowState = value(0)
            displayIndex = value(1)
        Catch ex As Exception
            Return Nothing
        End Try

        Dim imgSrc As Viewbox

        'server experiment?
        If displayIndex = -2 Then
            imgSrc = Application.Current.FindResource("icoDatabase")
            Return imgSrc
        End If

        'local experiment
        Select Case workFlowState
            Case WorkflowStatus.Finalized
                imgSrc = Application.Current.FindResource("icoOkBulletBlue")'
            Case WorkflowStatus.InProgress
                imgSrc = Application.Current.FindResource("icoRedDot")'
            Case WorkflowStatus.Unlocked
                imgSrc = Application.Current.FindResource("icoReopened") '
            Case Else
                imgSrc = Application.Current.FindResource("icoReopened") '
        End Select

        Return imgSrc

    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack

        Throw New NotImplementedException()

    End Function

End Class


Public Class UserTagConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.Convert

        Dim imgSrc As Viewbox

        Select Case CType(value, UserTag)
            Case UserTag.NoTag
                imgSrc = Nothing
            Case UserTag.Bookmarked
                '  imgSrc = Application.Current.FindResource("icoArrowRight")
                imgSrc = Application.Current.FindResource("icoPin")
            Case UserTag.Favorite
                imgSrc = Application.Current.FindResource("icoFavorite")
            Case Else
                imgSrc = Nothing
        End Select

        Return imgSrc

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.ConvertBack
        Return Nothing
    End Function

End Class


Public Class YieldStrConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.Convert

        Dim resStr As String
        Dim yield As Single = If(IsNothing(value), 0, value)

        If yield >= 10 Then
            resStr = ELNCalculations.SignificantDigitsString(yield, 3) + "%"
        ElseIf yield > 0 Then
            resStr = ELNCalculations.SignificantDigitsString(yield, 2) + "%"
        Else
            resStr = "no yield"
        End If

        Return resStr

    End Function

    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object _
    Implements IValueConverter.ConvertBack
        Return Nothing
    End Function

End Class


Public Class WeightFromGramsConverter

    Implements IValueConverter

    Public Shared Property SignificantDigits As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object _
        Implements IValueConverter.Convert

        Dim grams As Double = value

        Dim amountStr As String
        Dim unit As String

        Select Case grams
            Case 0  'when adding new
                Return ""
            Case >= 1000
                amountStr = SignificantDigitsString((grams / 1000), SignificantDigits)
                unit = WeightUnit.kg.ToString
            Case >= 1
                amountStr = SignificantDigitsString((grams), SignificantDigits)
                unit = WeightUnit.g.ToString
            Case Else
                amountStr = SignificantDigitsString((grams * 1000), SignificantDigits)
                unit = WeightUnit.mg.ToString
        End Select

        Return "(" + amountStr + ChrW(8201) + unit + ")"

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object _
        Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


'''<summary>
''' Determines on the fly if the data type of the current binding *ends with* the specified converter parameter string.
''' Example: For 'System.Collections.ArrayList', in addition to the complete type string, also the strings 'ArrayList' and 
''' 'Collections.ArrayList' will return true if passed as parameter strings.
'''</summary>
''' <remarks>
''' Allows type selective XAML databinding where multiple binding source types bind to an object, but conditions 
''' only should be checked for a specific data type. - Implement e.g. in a MultiDataTrigger as the first condition to test the 
''' {Binding} datatype using this converter, followed by a second condition actually testing for the binding value.
''' </remarks>
''' 
Public Class BindingTypeConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object _
    Implements IValueConverter.Convert

        If TypeOf parameter Is String Then
            Return value.GetType.ToString.EndsWith(parameter)
        Else
            Return False
        End If

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object _
    Implements IValueConverter.ConvertBack

        Return Nothing

    End Function
End Class

