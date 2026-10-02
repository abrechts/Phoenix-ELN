Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input

Public Enum MaterialCategory
    Reagent
    Solvent
    Auxiliary
End Enum

Public Enum EquivMatchMode
    Any
    AtLeast
    AtMost
    EqualTo
    Between
End Enum


''' <summary>
''' Reusable picker for a material category (reagent/solvent/auxiliary): lets the user locate a material
''' by name (with a prefix/substring toggle) from a supplied name list, optionally restrict by an equivalents
''' threshold, and raises a request to add the resulting combination as a search filter. dlgMaterialSearch
''' uses a single instance and re-points Category/NamesSource whenever the user switches category tabs.
''' </summary>
'''
Public Class MaterialNamePicker

    Inherits UserControl

    Public Event AddFilterRequested(sender As Object, category As MaterialCategory, materialName As String, mode As EquivMatchMode, value As Double?, valueTo As Double?)

    ''' <summary>
    ''' Sets or gets which material category this picker is currently locating names for. Purely
    ''' informational from this control's own point of view - it just gets echoed back in
    ''' AddFilterRequested so the owning dialog knows which category the added filter belongs to.
    ''' </summary>
    '''
    Public Property Category As MaterialCategory

    Private _namesView As ICollectionView


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        chkSubstring.IsChecked = My.Settings.dlgMaterialSearchPartialSearch

    End Sub


    ''' <summary>
    ''' Focuses the name-locate textbox - called by dlgMaterialSearch when it opens and whenever the
    ''' category (reagents/solvents/auxiliaries) is switched, so the user can start typing immediately.
    ''' </summary>
    '''
    Public Sub FocusLocateBox()

        txtLocate.Focus()

    End Sub


    ''' <summary>
    ''' Sets or gets the list of distinct material names (of this control's current Category) to locate from.
    ''' </summary>
    '''
    Public Property NamesSource As IEnumerable
        Get
            Return GetValue(NamesSourceProperty)
        End Get
        Set(value As IEnumerable)
            SetValue(NamesSourceProperty, value)
        End Set
    End Property

    Public Shared ReadOnly NamesSourceProperty As DependencyProperty = DependencyProperty.Register("NamesSource",
        GetType(IEnumerable), GetType(MaterialNamePicker), New PropertyMetadata(AddressOf OnNamesSourceChanged))

    Private Shared Sub OnNamesSourceChanged(o As DependencyObject, e As DependencyPropertyChangedEventArgs)

        Dim myCtrl = CType(o, MaterialNamePicker)
        myCtrl.lstNames.ItemsSource = e.NewValue

        If myCtrl.NamesSource IsNot Nothing Then
            myCtrl._namesView = CollectionViewSource.GetDefaultView(myCtrl.lstNames.ItemsSource)
            myCtrl._namesView.Filter = AddressOf myCtrl.NamesFilter
        End If

    End Sub


    Private Function NamesFilter(item As Object) As Boolean

        Dim name = CStr(item)
        Dim queryText = txtLocate.Text

        If String.IsNullOrEmpty(queryText) Then
            Return True
        ElseIf Not chkSubstring.IsChecked Then
            Return name.StartsWith(queryText, StringComparison.OrdinalIgnoreCase)
        ElseIf queryText.Length > 1 Then
            Return name.Contains(queryText, StringComparison.OrdinalIgnoreCase)
        Else
            Return False    'substring search: minimum filter length is 2 characters
        End If

    End Function


    Private Sub txtLocate_TextChanged() Handles txtLocate.TextChanged

        _namesView?.Refresh()

    End Sub


    Private Sub chkSubstring_Click() Handles chkSubstring.Click

        My.Settings.dlgMaterialSearchPartialSearch = chkSubstring.IsChecked
        _namesView?.Refresh()

    End Sub


    ''' <summary>
    ''' Adds a material as soon as the user clicks its row - no double-click needed. Reading SelectedItem
    ''' here is safe (unlike a double-click handler) because a ListBoxItem's selection updates on MouseDown,
    ''' before this MouseUp handler runs, so the clicked item is already SelectedItem by this point.
    ''' </summary>
    '''
    Private Sub lstNames_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstNames.MouseUp

        Dim matName = TryCast(lstNames.SelectedItem, String)
        RequestAddFilter(matName)

    End Sub


    ''' <summary>
    ''' Opens dlgEquivalentsFilter (starting at "any") to let the user specify the equivalents range as
    ''' part of adding the selected material - only raises AddFilterRequested if they confirm with OK.
    ''' Held while clicking a material row, Ctrl instead skips the dialog and adds the filter directly with
    ''' the default "any" equivalents range, for quick successive adds.
    ''' </summary>
    '''
    Private Sub RequestAddFilter(Optional selName As String = Nothing)

        If String.IsNullOrEmpty(selName) Then
            Exit Sub
        End If

        RaiseEvent AddFilterRequested(Me, Category, selName, EquivMatchMode.Any, Nothing, Nothing)

        'If Keyboard.Modifiers = ModifierKeys.Control Then
        '    RaiseEvent AddFilterRequested(Me, Category, selName, EquivMatchMode.Any, Nothing, Nothing)
        '    Exit Sub
        'End If

        'Dim editDlg As New dlgEquivalentsFilter With {
        '    .Owner = Window.GetWindow(Me),
        '    .Category = Category
        '}

        'If editDlg.ShowDialog() = True Then
        '    RaiseEvent AddFilterRequested(Me, Category, selName, editDlg.Mode, editDlg.Value, editDlg.ValueTo)
        'End If

    End Sub

End Class
