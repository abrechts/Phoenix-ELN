Imports System.ComponentModel
Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnBase.ELNCalculations
Imports ElnCoreModel


''' <summary>
''' Sorts navigation tree projects in descending SequenceNr order. This converter type generally provides otherwise
''' unavailable support for sorting Enums within HierarchicalDataTemplates.
''' </summary>
'''
Public Class ProjectsCollectionViewConverter

    Implements IValueConverter

    Public Property View As ListCollectionView

    ' Set by ExperimentTree's alphabetical-sort toolbar toggle. Kept on the converter (not the View) so it
    ' survives Convert re-running when tblProjects rebinds (e.g. switching the current user via cboUsers).
    Public Property SortAlphabetically As Boolean = False

    ' CustomSort with a typed comparer instead of SortDescriptions: SortDescriptions resolves "SequenceNr" via
    ' PropertyDescriptor reflection on every comparison, which since this Convert runs the first time any
    ' project's ItemsPresenter binds, is not gated by virtualization.

    Private Shared ReadOnly Comparer As IComparer = System.Collections.Generic.Comparer(Of tblProjects).Create(
        Function(a, b) System.Collections.Generic.Comparer(Of Short?).Default.Compare(b.SequenceNr, a.SequenceNr))

    Private Shared ReadOnly AlphaComparer As IComparer = System.Collections.Generic.Comparer(Of tblProjects).Create(
        Function(a, b) String.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase))

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        View = New ListCollectionView(CType(value, IList))
        View.CustomSort = If(SortAlphabetically, AlphaComparer, Comparer)
        Return View

    End Function


    ''' <summary>
    ''' Switches the project sort between alphabetical (by Title, ascending) and the default SequenceNr order.
    ''' </summary>
    '''
    Public Sub SetSortMode(alphabetical As Boolean)

        SortAlphabetically = alphabetical

        If View IsNot Nothing Then
            View.CustomSort = If(alphabetical, AlphaComparer, Comparer)
            View.Refresh()
        End If

    End Sub

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim view = CType(value, CollectionView)
        Return view.SourceCollection

    End Function

End Class


Public Class ProjectFoldersCollectionViewConverter

    Implements IValueConverter

    ' Unlike ProjectsCollectionViewConverter (one tblProjects collection per user, so a single View property is
    ' fine), this converter's Convert runs once per PROJECT (each has its own tblProjFolders collection), all
    ' sharing this one converter instance as a StaticResource. A single View property would just get overwritten
    ' by whichever project was bound last, so drag-drop reorder code has no reliable way to refresh the correct
    ' project's view afterward (SequenceNr changes don't auto-resort a CustomSort ListCollectionView). Keyed by
    ' source collection instead, so RefreshView can find the right one.
    Private ReadOnly _viewsBySource As New Dictionary(Of IList, ListCollectionView)

    ' See ProjectsCollectionViewConverter's Comparer field for why this avoids SortDescriptions.
    Private Shared ReadOnly Comparer As IComparer = System.Collections.Generic.Comparer(Of tblProjFolders).Create(
        Function(a, b) System.Collections.Generic.Comparer(Of Short?).Default.Compare(b.SequenceNr, a.SequenceNr))

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim sourceList = CType(value, IList)
        Dim view As New ListCollectionView(sourceList)
        view.CustomSort = Comparer
        _viewsBySource(sourceList) = view
        Return view

    End Function

    ''' <summary>
    ''' Re-sorts the already-realized view for the given project's folder collection, if one exists. Needed
    ''' after drag-drop reorder code changes SequenceNr directly on existing entities, since a CustomSort
    ''' ListCollectionView doesn't automatically notice property changes on its items.
    ''' </summary>
    '''
    Public Sub RefreshView(folders As IList)

        Dim view As ListCollectionView = Nothing
        If _viewsBySource.TryGetValue(folders, view) Then
            view.Refresh()
        End If

    End Sub

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

    ' See ProjectsCollectionViewConverter's Comparer field for why this avoids SortDescriptions - it's the sort
    ' that pays for every experiment in a folder, so it can dominate cost on a large project group.

    Private Shared ReadOnly Comparer As IComparer = System.Collections.Generic.Comparer(Of tblExperiments).Create(
        Function(a, b) System.Collections.Generic.Comparer(Of String).Default.Compare(b.ExperimentID, a.ExperimentID))

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        View = New ListCollectionView(CType(value, IList))
        View.CustomSort = Comparer
        Return View

    End Function

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        Dim view = CType(value, CollectionView)
        Return view.SourceCollection

    End Function

End Class



''' <summary>
''' Returns the first few project folders (sorted the same way as the tree, by descending SequenceNr),
''' capped at MaxItems. Used to keep the gong-wpf-dragdrop drag adorner preview for a dragged project
''' short instead of rendering its complete, potentially very long, list of folders.
''' </summary>
'''
Public Class DragPreviewFoldersConverter

    Implements IValueConverter

    Public Property MaxItems As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim folders = TryCast(value, IEnumerable(Of tblProjFolders))
        If folders Is Nothing Then Return Nothing

        Return folders.OrderByDescending(Function(f) f.SequenceNr).Take(MaxItems).ToList()

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


''' <summary>
''' Returns the first few experiments (sorted the same way as the tree, by descending ExperimentID),
''' capped at MaxItems. Used to keep the gong-wpf-dragdrop drag adorner preview for a dragged folder
''' short instead of rendering its complete, potentially very long, list of experiments.
''' </summary>
'''
Public Class DragPreviewExperimentsConverter

    Implements IValueConverter

    Public Property MaxItems As Integer = 3

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim experiments = TryCast(value, IEnumerable(Of tblExperiments))
        If experiments Is Nothing Then Return Nothing

        Return experiments.OrderByDescending(Function(exp) exp.ExperimentID).Take(MaxItems).ToList()

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


''' <summary>
''' Returns a "+ N more ..." hint string for the items beyond ConverterParameter (the same cap applied by
''' DragPreviewFoldersConverter/DragPreviewExperimentsConverter), or an empty string if nothing was cut off.
''' Pair with StringToVisibilityConverter to hide the hint when empty.
''' </summary>
'''
Public Class RemainingItemsSummaryConverter

    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim items = TryCast(value, ICollection)
        If items Is Nothing Then Return ""

        Dim maxCount As Integer = System.Convert.ToInt32(parameter)
        Dim remaining = items.Count - maxCount

        Return If(remaining > 0, $"+ {remaining} more ...", "")

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function

End Class


''' <summary>
''' Selects the lightweight header-only drag adorner preview (dd:DragDrop.DragAdornerTemplateSelector) matching the
''' dragged item's type, so the gong-wpf-dragdrop preview shows only a capped summary instead of the default full
''' screen-capture adorner (which renders the item's complete, currently rendered subtree as-is).
''' </summary>
'''
Public Class DragAdornerTemplateSelector

    Inherits DataTemplateSelector

    Public Property ProjectTemplate As DataTemplate
    Public Property FolderTemplate As DataTemplate
    Public Property ExperimentTemplate As DataTemplate

    Public Overrides Function SelectTemplate(item As Object, container As DependencyObject) As DataTemplate

        Select Case True
            Case TypeOf item Is tblProjects
                Return ProjectTemplate
            Case TypeOf item Is tblProjFolders
                Return FolderTemplate
            Case TypeOf item Is tblExperiments
                Return ExperimentTemplate
            Case Else
                Return Nothing
        End Select

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

