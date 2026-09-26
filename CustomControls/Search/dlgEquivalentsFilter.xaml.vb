Imports System.Windows

''' <summary>
''' Small modal for specifying an equivalents comparison (any/greater-or-equal/less-or-equal/exactly-equal/
''' between) plus its one or two threshold values. Reused both when adding a new material filter
''' (MaterialNamePicker.RequestAddFilter) and when editing an existing filter chip
''' (dlgMaterialSearch.ChipBody_PreviewMouseUp) - callers set Category/Mode/Value/ValueTo before
''' ShowDialog(), and read them back only if it returns True.
''' </summary>
'''
Public Class dlgEquivalentsFilter

    Public Property Category As MaterialCategory

    Public Property Mode As EquivMatchMode
    Public Property Value As Double?
    Public Property ValueTo As Double?


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        Dim unitLabel = UnitLabelFor(Category)
        Dim unitTooltip = UnitTooltipFor(Category)
        blkUnit.Text = unitLabel
        blkUnit.ToolTip = unitTooltip
        blkRangeUnit.Text = unitLabel
        blkRangeUnit.ToolTip = unitTooltip

        txtValue.Value = Value
        txtValueFrom.Value = Value
        txtValueTo.Value = ValueTo

        Select Case Mode
            Case EquivMatchMode.AtLeast : rbAtLeast.IsChecked = True
            Case EquivMatchMode.AtMost : rbAtMost.IsChecked = True
            Case EquivMatchMode.EqualTo : rbEqualTo.IsChecked = True
            Case EquivMatchMode.Between : rbBetween.IsChecked = True
            Case Else : rbAny.IsChecked = True
        End Select

        UpdateValuePanelVisibility()
        UpdateOkEnabled()

    End Sub


    Private Shared Function UnitLabelFor(category As MaterialCategory) As String

        Select Case category
            Case MaterialCategory.Reagent : Return "eq"
            Case MaterialCategory.Solvent : Return "vq"
            Case Else : Return "wq"
        End Select

    End Function


    Private Shared Function UnitTooltipFor(category As MaterialCategory) As String

        Select Case category
            Case MaterialCategory.Reagent : Return "equivalents"
            Case MaterialCategory.Solvent : Return "volume equivalents"
            Case Else : Return "weight equivalents"
        End Select

    End Function


    Private Sub ModeToggle_Checked() Handles rbAny.Checked, rbAtLeast.Checked, rbAtMost.Checked, rbEqualTo.Checked, rbBetween.Checked

        If Not IsLoaded Then
            'rbAny's IsChecked="True" in XAML fires this Checked event during InitializeComponent, before
            'the other elements referenced below are connected - Me_Loaded runs the same logic once
            'everything (and the caller's Mode/Value/ValueTo) is actually in place.
            Exit Sub
        End If

        UpdateValuePanelVisibility()
        UpdateOkEnabled()

    End Sub


    Private Sub UpdateValuePanelVisibility()

        pnlRangeValue.Visibility = If(rbBetween.IsChecked, Visibility.Visible, Visibility.Collapsed)
        pnlSingleValue.Visibility = If(rbAny.IsChecked OrElse rbBetween.IsChecked, Visibility.Collapsed, Visibility.Visible)

        If rbBetween.IsChecked Then
            txtValueFrom.Focus()
        ElseIf Not rbAny.IsChecked Then
            txtValue.Focus()
        End If

    End Sub


    Private Sub ValueFields_TextChanged() Handles txtValue.TextChanged, txtValueFrom.TextChanged, txtValueTo.TextChanged

        UpdateOkEnabled()

    End Sub


    ''' <summary>
    ''' A threshold-requiring mode needs its value(s) filled in before OK is allowed; "between" additionally
    ''' requires the lower bound not to exceed the upper one.
    ''' </summary>
    '''
    Private Sub UpdateOkEnabled()

        If rbAny.IsChecked Then
            btnOK.IsEnabled = True
        ElseIf rbBetween.IsChecked Then
            btnOK.IsEnabled = txtValueFrom.Value.HasValue AndAlso txtValueTo.Value.HasValue AndAlso txtValueFrom.Value.Value <= txtValueTo.Value.Value
        Else
            btnOK.IsEnabled = txtValue.Value.HasValue
        End If

    End Sub


    Private Sub btnOK_Click() Handles btnOK.Click

        If rbAny.IsChecked Then
            Mode = EquivMatchMode.Any
            Value = Nothing
            ValueTo = Nothing
        ElseIf rbBetween.IsChecked Then
            Mode = EquivMatchMode.Between
            Value = txtValueFrom.Value
            ValueTo = txtValueTo.Value
        Else
            Mode = If(rbAtLeast.IsChecked, EquivMatchMode.AtLeast, If(rbAtMost.IsChecked, EquivMatchMode.AtMost, EquivMatchMode.EqualTo))
            Value = txtValue.Value
            ValueTo = Nothing
        End If

        DialogResult = True

    End Sub

End Class
