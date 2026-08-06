Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media


Public Class TreeViewEditLabel

    Public Event RequestValidation(sender As Object, newText As String, ByRef isValid As Boolean)

    Private WithEvents ClickDelayTimer As New Threading.DispatcherTimer
    Private WithEvents ParentTreeViewItem As TreeViewItem

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
      GetType(String), GetType(TreeViewEditLabel), metaData)

    Public Property EditText() As String
        Get
            Return DirectCast(GetValue(EditTextProperty), String)
        End Get
        Set(value As String)
            SetValue(EditTextProperty, value)
        End Set
    End Property

    Private Shared Sub OnTextChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
    End Sub


    Public Shared ReadOnly TitleForegroundProperty As DependencyProperty = DependencyProperty.Register(
      "TitleForeground", GetType(Brush), GetType(TreeViewEditLabel),
      New FrameworkPropertyMetadata(SystemColors.ControlTextBrush))

    Public Property TitleForeground As Brush
        Get
            Return DirectCast(GetValue(TitleForegroundProperty), Brush)
        End Get
        Set(value As Brush)
            SetValue(TitleForegroundProperty, value)
        End Set
    End Property


    Private Sub Me_Loaded() Handles Me.Loaded

        ClickDelayTimer.Interval = TimeSpan.FromMilliseconds(DoubleClickDelay)
        ParentTreeViewItem = WPFToolbox.FindVisualParent(Of TreeViewItem)(Me)

        txtTitle.MaxLength = MaxCharacters

    End Sub


    ''' <summary>
    ''' This shared property gets the TreeViewEditLabel currently in edit mode. Useful for
    ''' for e.g. for validating the currently focused control in the application close event,
    ''' or for preventing mouse clicks on other controls when in edit.
    ''' </summary>
    '''
    Public Shared Property CurrentEditItem As TreeViewEditLabel = Nothing


    ''' <summary>
    ''' Sets or gets if the control currently is in edit mode.
    ''' </summary>
    '''
    Public Property IsInEdit As Boolean = False


    ''' <summary>
    ''' Sets or gets if the control has obtained a first mouse click (the second one will enter edit mode).
    ''' </summary>
    '''
    Private Property HasFirstMouseClick As Boolean = False


    ''' <summary>
    ''' Sets or gets if the time after a first click still is within the double-click time range.
    ''' </summary>
    '''
    Private Property IsInDoubleClickRange As Boolean = True


    ''' <summary>
    ''' Sets or gets the timespan in milliseconds which is considered the maximum double click
    ''' time interval. The default is 400 ms.
    ''' </summary>
    '''
    Public Property DoubleClickDelay As Double = 400   'ms


    ''' <summary>
    ''' Sets or gets the maximum allowed characters for user input. The default value
    ''' of zero means no restriction.
    ''' </summary>
    '''
    Public Property MaxCharacters As Integer = 0


    Private ReadOnly Property EditBorderBrush As Brush

        Get
            If DarkModeHelper.GetIsDarkMode(Me) Then
                Return New SolidColorBrush(ColorConverter.ConvertFromString("#FF8FC4F8"))
            End If
            Return New SolidColorBrush(ColorConverter.ConvertFromString("#FF4A90D9"))
        End Get

    End Property


    Private Sub ClickDelayTimer_Tick() Handles ClickDelayTimer.Tick

        IsInDoubleClickRange = False
        ClickDelayTimer.Stop()

    End Sub


    Private Sub parentTreeViewItem_Unselected(sender As Object, e As RoutedEventArgs) Handles ParentTreeViewItem.Unselected

        HasFirstMouseClick = False
        displayBorder.ClearValue(Border.BorderBrushProperty)
        txtTitle.ClearValue(TextBox.BorderBrushProperty)

    End Sub


    Private Sub ParentTreeViewItem_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles ParentTreeViewItem.PreviewKeyDown

        If e.Key = Key.Escape AndAlso HasFirstMouseClick AndAlso Not IsInEdit Then
            HasFirstMouseClick = False
            displayBorder.ClearValue(Border.BorderBrushProperty)
            txtTitle.ClearValue(TextBox.BorderBrushProperty)
        End If

    End Sub


    ''' <summary>
    ''' Click detection happens on the display-mode TextBlock (txtDisplay), not on txtTitle itself:
    ''' txtTitle is a TextBoxBase, which gong-wpf-dragdrop unconditionally excludes from drag
    ''' initiation via its built-in hit-test check - keeping it collapsed until edit mode lets
    ''' drag start normally while hovering the title text.
    ''' </summary>
    '''
    Private Sub txtDisplay_PreviewMouseLeftButtonUp() Handles txtDisplay.PreviewMouseLeftButtonUp

        If Not HasFirstMouseClick Then

            IsInDoubleClickRange = True
            ClickDelayTimer.Start()
            HasFirstMouseClick = True
            displayBorder.BorderBrush = EditBorderBrush
            txtTitle.BorderBrush = EditBorderBrush

        Else

            If Not IsInDoubleClickRange Then
                BeginEdit()
            Else
                HasFirstMouseClick = False
                displayBorder.ClearValue(Border.BorderBrushProperty)
                txtTitle.ClearValue(TextBox.BorderBrushProperty)
            End If

        End If

    End Sub


    ''' <summary>
    ''' Commit text data when Return or Enter key is pressed, perform Undo if ESC key is pressed.
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

        txtTitle.IsReadOnly = False

        WPFToolbox.WaitForPriority(Threading.DispatcherPriority.ContextIdle)

        With txtTitle
            .Focus()
            .Select(0, 255)
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

        ' Update source from code to cover situations where txtTitle does not lose focus
        Dim expr = txtTitle.GetBindingExpression(TextBox.TextProperty)
        expr.UpdateSource()

        Dim isValid As Boolean = True
        RaiseEvent RequestValidation(Me, txtTitle.Text, isValid)

        If isValid Then

            ' A parent validation logic is expected to handle the validation event according to its rules.
            ' If the event is not handled, the validation is assumed valid.

            txtTitle.IsReadOnly = True
            displayBorder.ClearValue(Border.BorderBrushProperty)
            txtTitle.ClearValue(TextBox.BorderBrushProperty)
            HasFirstMouseClick = False
            IsInEdit = False

            CurrentEditItem = Nothing

        End If

        Return isValid

    End Function

End Class


