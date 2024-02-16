Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnCoreModel

Public Class SearchCombo


    Public Event MaterialSelected(sender As Object, selectedItem As tblMaterials)

    Public Event RequestDeleteItem(sender As Object, targetItem As tblMaterials)


    Private Property ItemsCollectionView As CollectionView


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        chkSubString.IsChecked = My.Settings.PartialMatSearch

    End Sub


    ''' <summary>
    ''' Sets or gets the collection of items to select from in the dropdown list (dependency property).
    ''' </summary>
    ''' 
    Public Property QueryItemsSource As IEnumerable
        Get
            Return GetValue(QueryItemsSourceProperty)
        End Get

        Set(ByVal value As IEnumerable)
            SetValue(QueryItemsSourceProperty, value)
        End Set
    End Property

    Public Shared ReadOnly QueryItemsSourceProperty As DependencyProperty = DependencyProperty.Register("QueryItemsSource",
       GetType(IEnumerable), GetType(SearchCombo), New PropertyMetadata(AddressOf OnQueryItemsSourceChanged))

    Private Shared Sub OnQueryItemsSourceChanged(ByVal o As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)

        Dim myCtrl = CType(o, SearchCombo)

        With myCtrl
            .cboSearch.ItemsSource = e.NewValue
            If .QueryItemsSource IsNot Nothing Then
                .ItemsCollectionView = CollectionViewSource.GetDefaultView(.cboSearch.ItemsSource)
                .ItemsCollectionView.Filter = AddressOf .ItemCollView_Filter
            End If
        End With

    End Sub


    ''' <summary>
    ''' Core: Filter query items
    ''' </summary>
    ''' 
    Private Function ItemCollView_Filter(item As tblMaterials) As Boolean

        Dim queryText = txtSearch.Text

        If Not String.IsNullOrEmpty(queryText) Then
            If Not chkSubString.IsChecked Then
                Return item.MatName.StartsWith(queryText, True, Nothing)
            Else
                If queryText.Length > 1 Then
                    Return item.MatName.Contains(queryText, StringComparison.CurrentCultureIgnoreCase)
                Else
                    Return False   'substring search: minimum filter length is 2 characters
                End If
            End If
        Else
            Return False
        End If


    End Function


    ''' <summary>
    ''' Gets if the materials hit dropdown menu currently is open.
    ''' </summary>
    ''' 
    Public ReadOnly Property IsDropDownOpen As Boolean
        Get
            Return cboSearch.IsDropDownOpen
        End Get
    End Property


    ''' <summary>
    ''' Sets or gets the property of the query items to display and to
    ''' to filter for in the dropdown list.
    ''' </summary>
    ''' 
    Public Property SearchPropertyName As String

        Get
            Return cboSearch.DisplayMemberPath
        End Get

        Set(value As String)
            cboSearch.DisplayMemberPath = value
        End Set

    End Property


    ''' <summary>
    ''' Enters edit mode and places the cursor at the end of text, if present.
    ''' </summary>
    ''' 
    Public Sub ActivateEdit()

        txtSearch.Focus()
        txtSearch.Select(255, 0)

    End Sub


    ''' <summary>
    ''' Sets or gets the query text. Also text not matching any of the QueryItemSource elements 
    ''' can be entered.
    ''' </summary>
    ''' 
    Public Property Text As String

        Get
            Return txtSearch.Text
        End Get
        Set

            Dim origStr = txtSearch.Text
            txtSearch.Text = Value
            If origStr = "" Then
                ItemsCollectionView?.Refresh()
            End If

        End Set

    End Property


    Private Sub txtSearch_TextChanged() Handles txtSearch.TextChanged

        If txtSearch.IsKeyboardFocused Then

            With cboSearch

                ItemsCollectionView.Refresh()

                If .Items.Count > 0 Then
                    If txtSearch.Text.ToLower <> .Items(0).MatName.ToLower Then
                        'partial match
                        If txtSearch.Text <> "" Then
                            DropDownScrollViewer.ScrollToTop()
                            .SelectedIndex = 0
                            .IsDropDownOpen = True
                            RaiseEvent MaterialSelected(Me, Nothing)
                        End If
                    Else
                        'full match
                        .IsDropDownOpen = True
                        .SelectedItem = .Items(0)
                        txtSearch.Select(255, 1)
                        RaiseEvent MaterialSelected(Me, .Items(0))
                    End If
                Else
                    .IsDropDownOpen = False
                    RaiseEvent MaterialSelected(Me, Nothing)
                End If
            End With

        End If

    End Sub


    ''' <summary>
    ''' Handles toggling between standard and substring name query.
    ''' </summary>
    ''' 
    Private Sub chkSubstring_Click() Handles chkSubString.Click

        ItemsCollectionView.Refresh()
        txtSearch.Focus()

        If cboSearch.Items.Count > 0 AndAlso txtSearch.Text <> "" Then
            cboSearch.IsDropDownOpen = True
        End If

        My.Settings.PartialMatSearch = chkSubString.IsChecked

    End Sub


    ''' <summary>
    ''' Handles click on delete button in materials dropdown menu item.
    ''' </summary>
    ''' 
    Private Sub cboItem_DelClick(sender As Object, e As RoutedEventArgs)

        If cboSearch.Items.Count > 0 Then

            Dim matchItem = CType(sender.DataContext, tblMaterials)

            'locally remove item from dropdown list
            CType(cboSearch.ItemsSource, List(Of tblMaterials)).Remove(matchItem)
            ItemsCollectionView.Refresh()

            If txtSearch.Text = matchItem.MatName Then
                Text = ""
            End If

            'request removal from actual source list
            RaiseEvent RequestDeleteItem(Me, matchItem)

            _IsDeleting = True

        End If

    End Sub

    Private _IsDeleting As Boolean = False


    ''' <summary>
    ''' Raises the MaterialSelected event if a specific material dropdown item is clicked.
    ''' </summary>
    ''' 
    Private Sub cboItem_PreviewMouseUp(sender As Object, e As RoutedEventArgs)    'XAML event

        If Not _IsDeleting Then

            Dim matEntry As tblMaterials = sender.DataContext

            If matEntry IsNot Nothing Then

                txtSearch.Text = matEntry.MatName
                RaiseEvent MaterialSelected(Me, matEntry)

            End If

        Else

            _IsDeleting = False

        End If

    End Sub


    ''' <summary>
    ''' Handle keyDown events
    ''' </summary>
    '''
    Private Sub txtSearch_PreviewKeyUp(sender As Object, e As KeyEventArgs) Handles txtSearch.PreviewKeyDown

        With cboSearch

            Select Case e.Key

                Case Key.Return, e.Key = Key.Enter

                    If cboSearch.SelectedItem IsNot Nothing Then
                        Dim matEntry As tblMaterials = cboSearch.SelectedItem
                        txtSearch.Text = matEntry.MatName
                        .IsDropDownOpen = False
                        RaiseEvent MaterialSelected(Me, matEntry)
                    End If

                Case Key.Down

                    If .IsDropDownOpen AndAlso .Items.Count > 0 Then
                        .SelectedIndex += 1 'ignored if index higher than number of elements
                    End If

                Case Key.Up

                    If .SelectedIndex > 0 Then
                        .SelectedIndex -= 1
                    End If

                Case Key.Escape

                    .IsDropDownOpen = False

            End Select

        End With

    End Sub


    ''' <summary>
    ''' Re-focuses on edit TextBox after popup selection
    ''' </summary>
    ''' 
    Private Sub cboSearch_PreviewKeyUp(sender As Object, e As KeyEventArgs) Handles cboSearch.PreviewKeyUp

        If e.Key = Key.Return OrElse e.Key = Key.Enter Then
            txtSearch.Focus()
            txtSearch.Select(255, 0)
        End If

    End Sub


    Private ReadOnly Property DropDownScrollViewer As ScrollViewer

        Get
            Return CType(WPFToolbox.FindVisualChild(Of ScrollViewer)(cboSearch), ScrollViewer)
        End Get

    End Property


    Private Sub cboSearch_DropdownClosed() Handles cboSearch.DropDownClosed

        txtSearch.Focus()
        txtSearch.Select(255, 0)

    End Sub


End Class


Public Class AddMatButtonEnabledConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert

        If values(0) Is DependencyProperty.UnsetValue Then
            Return Nothing
        End If

        Dim cboItemsCount As Integer = values(0)
        Dim searchText As String = values(1)

        Return cboItemsCount=1 


    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
