Imports System.Globalization
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Media


Public Class ListBoxEditLabel

    Private WithEvents ParentListBoxItem As ListBoxItem


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub

    ''' <summary>
    ''' Sets or gets the displayed text
    ''' </summary>

    Private Shared metaData = New FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
      New PropertyChangedCallback(AddressOf OnTextChanged))

    Public Shared ReadOnly EditTextProperty As DependencyProperty = DependencyProperty.Register("EditText",
      GetType(String), GetType(ListBoxEditLabel), metaData)

    Public Property EditText() As String
        Get
            Return DirectCast(GetValue(EditTextProperty), String)
        End Get
        Set(value As String)
            SetValue(EditTextProperty, value)
        End Set
    End Property

    Private Shared Sub OnTextChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        ' Dim editBlock = DirectCast(d, EditableTextBlock)
    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        ParentListBoxItem = WPFToolbox.FindVisualParent(Of ListBoxItem)(Me)
        txtTitle.MaxLength = MaxAllowedCharacters

    End Sub


    ''' <summary>
    ''' This shared property gets the ListBoxEditLabel currently in edit mode. Useful for 
    ''' for e.g. for validating the currently focused control in the application close event, 
    ''' or for preventing mouse clicks on other controls when in edit.
    ''' </summary>
    ''' 
    Public Shared Property CurrentEditItem As ListBoxEditLabel = Nothing


    ''' <summary>
    ''' Sets or gets if the control currently is in edit mode.
    ''' </summary>
    ''' 
    Public Property IsInEdit As Boolean = False


    ''' <summary>
    ''' Sets or gets if an empty string is allowed for EditText.
    ''' </summary>
    ''' 
    Public Property IsEmptyStringAllowed As Boolean = False


    ''' <summary>
    ''' Sets or gets the maximum allowed characters for user input. The default value 
    ''' of zero means no restriction.
    ''' </summary>
    ''' 
    Public Property MaxAllowedCharacters As Integer = 0


    ''' <summary>
    ''' Sets or gets if the control has obtained a first mouse click (the second one will enter edit mode).
    ''' </summary>
    '''
    Private Property HasFirstMouseClick As Boolean = False




    Private Sub parentListBoxItem_Selected(sender As Object, e As RoutedEventArgs) Handles ParentListBoxItem.Selected

        If Me.IsMouseOver Then

            HasFirstMouseClick = True

        End If

    End Sub

    Private Sub parentListBoxItem_Unselected(sender As Object, e As RoutedEventArgs) Handles ParentListBoxItem.Unselected

        txtTitle.Background = Brushes.White
        HasFirstMouseClick = False

    End Sub


    Private Sub blkCover_MouseLeftButtonDown() Handles blkCover.PreviewMouseLeftButtonDown

        If Not HasFirstMouseClick Then

            HasFirstMouseClick = True
            txtTitle.Background = New SolidColorBrush(ColorConverter.ConvertFromString("#FFE2E2E2"))

        Else

            BeginEdit()

        End If

    End Sub


    ''' <summary>
    ''' Commit text data when Return or Enter key is pressed, perform Undo is ESC key is pressed.
    ''' </summary>
    ''' 
    Private Sub txtTitle_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtTitle.PreviewKeyDown

        If e.Key = Key.Enter OrElse e.Key = Key.Return Then
            EndEdit()

        ElseIf e.Key = Key.Escape Then
            txtTitle.Undo()
            EndEdit()

        End If

    End Sub


    ''' <summary>
    ''' Restores the original value
    ''' </summary>
    ''' 
    Public Sub Undo()

        txtTitle.Undo()

        Dim expr = txtTitle.GetBindingExpression(TextBox.TextProperty)
        expr.UpdateSource()

    End Sub


    ''' <summary>
    ''' Sets the control into edit mode.
    ''' </summary>
    ''' 
    Public Sub BeginEdit()

        blkCover.Visibility = Visibility.Collapsed
        txtTitle.Background = FindResource("ActiveEditBackground")

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        With txtTitle
            .Focus()
            .Select(255, 0)
        End With

        HasFirstMouseClick = True
        IsInEdit = True

        CurrentEditItem = Me

    End Sub


    ''' <summary>
    ''' Ends the edit mode.
    ''' </summary>
    ''' 
    Public Function EndEdit() As Boolean

        If txtTitle.Text <> "" OrElse IsEmptyStringAllowed Then

            'update source from code to cover situations where txtTitle does not lose focus
            Dim expr = txtTitle.GetBindingExpression(TextBox.TextProperty)
            expr.UpdateSource()
            Keyboard.ClearFocus()

            txtTitle.Background = Brushes.White
            blkCover.Visibility = Visibility.Visible
            HasFirstMouseClick = False
            IsInEdit = False

            CurrentEditItem = Nothing

            Return True

        Else

            MsgBox("Please enter some text!", MsgBoxStyle.Information, "Validation")
            Return False

        End If


    End Function

End Class


