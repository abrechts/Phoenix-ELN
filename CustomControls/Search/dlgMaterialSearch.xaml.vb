Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class dlgMaterialSearch

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)

    Public Property LocalDBContext As ElnDbContext
    Public Property ServerDBContext As ElnDbContext
    Private Property SearchContext As ElnDbContext

    Private _filterRows As ObservableCollection(Of MaterialFilterRow)

    Private _reagentNames As List(Of String)
    Private _solventNames As List(Of String)
    Private _auxiliaryNames As List(Of String)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        _filterRows = New ObservableCollection(Of MaterialFilterRow)
        icFilters.ItemsSource = _filterRows

        AddHandler picker.AddFilterRequested, AddressOf Picker_AddFilterRequested

        cboSorting.SelectedIndex = If(My.Settings.dlgMaterialSearchSortByYield, 0, 1)

        If ServerDBContext Is Nothing Then
            chkServerSearch.IsEnabled = False
            chkServerSearch.IsChecked = False
        Else
            chkServerSearch.IsChecked = My.Settings.IsServerQuery
        End If

        InitializeSearchContext(chkServerSearch.IsChecked)

        picker.FocusLocateBox()

    End Sub


    Private Sub Me_Closing() Handles Me.Closing

        My.Settings.dlgMaterialSearchPosition = New System.Drawing.Point(Left, Top)
        My.Settings.dlgMaterialSearchSize = New System.Drawing.Size(ActualWidth, ActualHeight)

    End Sub


    Private Sub InitializeSearchContext(isServerContext As Boolean)

        SearchContext = If(isServerContext, ServerDBContext, LocalDBContext)

        'filter rows only store category/name/equivalents (not experiment IDs), so they stay valid and are
        'simply re-evaluated against the newly selected context
        LoadMaterialNames()
        UpdateResults()

    End Sub


    ''' <summary>
    ''' (Re)loads the distinct, case-insensitively de-duplicated material names actually used in the
    ''' current search context's reagents/solvents/auxiliaries tables.
    ''' </summary>
    '''
    Private Sub LoadMaterialNames()

        _reagentNames = DistinctNames(SearchContext.tblReagents.Select(Function(r) r.Name))
        _solventNames = DistinctNames(SearchContext.tblSolvents.Select(Function(s) s.Name))
        _auxiliaryNames = DistinctNames(SearchContext.tblAuxiliaries.Select(Function(a) a.Name))

        picker.NamesSource = NamesFor(picker.Category)

    End Sub


    Private Function NamesFor(category As MaterialCategory) As List(Of String)

        Select Case category
            Case MaterialCategory.Reagent : Return _reagentNames
            Case MaterialCategory.Solvent : Return _solventNames
            Case Else : Return _auxiliaryNames
        End Select

    End Function


    Private Function DistinctNames(namesQuery As IQueryable(Of String)) As List(Of String)

        Return namesQuery.
            Distinct().
            ToList().
            GroupBy(Function(n) n.ToUpperInvariant()).
            Select(Function(g) g.First()).
            OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).
            ToList()

    End Function


    Private Sub CategoryToggle_Checked() Handles rbReagents.Checked, rbSolvents.Checked, rbAuxiliaries.Checked

        If picker Is Nothing Then
            Exit Sub
        End If

        Dim category = If(rbReagents.IsChecked, MaterialCategory.Reagent, If(rbSolvents.IsChecked, MaterialCategory.Solvent, MaterialCategory.Auxiliary))

        picker.Category = category
        picker.NamesSource = NamesFor(category)
        picker.FocusLocateBox()

    End Sub


    Private Sub chkServerSearch_CheckedChanged() Handles chkServerSearch.Checked, chkServerSearch.Unchecked

        If chkServerSearch.IsInitialized Then

            InitializeSearchContext(chkServerSearch.IsChecked)

            'update setting after *manual* change only, i.e. not when server currently unavailable
            If chkServerSearch.IsMouseOver Then
                My.Settings.IsServerQuery = chkServerSearch.IsChecked
            End If

        End If

    End Sub


    Private Sub cboSorting_SelectionChanged() Handles cboSorting.SelectionChanged

        If _filterRows Is Nothing Then
            'cboSorting's SelectedIndex="0" in XAML fires this event during InitializeComponent, before
            'Me_Loaded has created _filterRows - nothing to (re-)sort yet, so it's safe to skip.
            Exit Sub
        End If

        My.Settings.dlgMaterialSearchSortByYield = (cboSorting.SelectedIndex = 0)
        UpdateResults()

    End Sub


    ''' <summary>
    ''' Upper bound on simultaneously added filters - generous enough that it will rarely be reached in
    ''' practice, but still keeps the query (an intersection across all filters) and the chip list bounded.
    ''' </summary>
    '''
    Private Const MaxFilterCount As Integer = 10


    Private Sub Picker_AddFilterRequested(sender As Object, category As MaterialCategory, materialName As String, mode As EquivMatchMode, value As Double?, valueTo As Double?)

        If _filterRows.Count >= MaxFilterCount Then
            cbMsgBox.Display($"A maximum of {MaxFilterCount} filters can be added at once.", MsgBoxStyle.Information, "Search by Materials")
            Exit Sub
        End If

        'avoid adding a duplicate filter for the same category + material name
        If _filterRows.Any(Function(r) r.Category = category AndAlso String.Equals(r.MaterialName, materialName, StringComparison.OrdinalIgnoreCase)) Then
            Exit Sub
        End If

        _filterRows.Add(New MaterialFilterRow With {
            .Category = category,
            .MaterialName = materialName,
            .Mode = mode,
            .Value = value,
            .ValueTo = valueTo
        })

        UpdateResults()

    End Sub


    Private Sub RemoveFilterChip_Click(sender As Object, e As RoutedEventArgs)

        Dim row = TryCast(CType(sender, Button).Tag, MaterialFilterRow)

        If row IsNot Nothing Then
            _filterRows.Remove(row)
            UpdateResults()
        End If

    End Sub


    ''' <summary>
    ''' Opens dlgEquivalentsFilter, pre-filled with this chip's current equivalents range, so the user can
    ''' adjust it - applies the result back to the row (and re-runs the search) only if they confirm with OK.
    ''' </summary>
    '''
    Private Sub ChipBody_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs)

        Dim row = TryCast(CType(sender, FrameworkElement).DataContext, MaterialFilterRow)

        If row Is Nothing Then
            Exit Sub
        End If

        Dim editDlg As New dlgEquivalentsFilter With {
            .Owner = Me,
            .Category = row.Category,
            .Mode = row.Mode,
            .Value = row.Value,
            .ValueTo = row.ValueTo
        }

        If editDlg.ShowDialog() = True Then
            row.Mode = editDlg.Mode
            row.Value = editDlg.Value
            row.ValueTo = editDlg.ValueTo
            UpdateResults()
        End If

    End Sub


    ''' <summary>
    ''' Re-runs the material filter query and refreshes the results list. An experiment matches only if it
    ''' contains every currently added filter (AND semantics) - each filter's matching experiment ID set is
    ''' intersected with the others, mirroring how dlgTagSearch combines multiple tag filters.
    ''' </summary>
    '''
    Private Sub UpdateResults()

        If _filterRows.Count = 0 Then
            lstResults.ItemsSource = Nothing
            blkHitInfo.Text = ""
            blkPlaceholder.Text = "---  add filters to search  ---"
            Exit Sub
        End If

        Dim matchingExpIds As HashSet(Of String) = Nothing

        For Each row In _filterRows

            Dim rowIds = GetMatchingExpIds(row)
            matchingExpIds = If(matchingExpIds Is Nothing, rowIds, New HashSet(Of String)(matchingExpIds.Intersect(rowIds)))

            If matchingExpIds.Count = 0 Then
                Exit For
            End If

        Next

        Dim matchingExpIdList = matchingExpIds.ToList()

        'restricted to finalized experiments, matching the RSS/full-text search convention (see
        'RxnSubstructure.vb/FullTextSearch.vb) - tag search is the one deliberate exception, not this
        Dim unsortedMatches = SearchContext.tblExperiments.
            Where(Function(exp) matchingExpIdList.Contains(exp.ExperimentID) AndAlso exp.WorkflowState = WorkflowStatus.Finalized).
            ToList()

        Dim sortedMatches = If(cboSorting.SelectedIndex = 0,
            unsortedMatches.OrderByDescending(Function(exp) exp.Yield).ToList(),
            unsortedMatches.OrderByDescending(Function(exp) exp.RefReactantGrams).ToList())

        'cap displayed hits like SearchBase.MaxDisplayedResults - some materials (e.g. water, used
        'during workup) occur in nearly every experiment and would otherwise flood the results list
        Dim wasTruncated = sortedMatches.Count > SearchBase.MaxDisplayedResults
        Dim matches = If(wasTruncated, sortedMatches.Take(SearchBase.MaxDisplayedResults).ToList(), sortedMatches)

        lstResults.ItemsSource = matches
        blkHitInfo.Text = SearchBase.BuildHitCountText(matches.Count, wasTruncated)
        blkPlaceholder.Text = "---  no matching finalized experiments  ---"

    End Sub


    ''' <summary>
    ''' Gets the set of experiment IDs containing a material matching the given filter row's category,
    ''' name (case-insensitive) and equivalents range.
    ''' </summary>
    '''
    Private Function GetMatchingExpIds(row As MaterialFilterRow) As HashSet(Of String)

        Dim upperName = row.MaterialName.ToUpperInvariant()

        Select Case row.Category

            Case MaterialCategory.Reagent

                Dim query = SearchContext.tblReagents.Where(Function(r) r.Name.ToUpper() = upperName)
                Select Case row.Mode
                    Case EquivMatchMode.EqualTo
                        If row.Value.HasValue Then query = query.Where(Function(r) r.Equivalents = row.Value.Value)
                    Case EquivMatchMode.AtLeast
                        If row.Value.HasValue Then query = query.Where(Function(r) r.Equivalents >= row.Value.Value)
                    Case EquivMatchMode.AtMost
                        If row.Value.HasValue Then query = query.Where(Function(r) r.Equivalents <= row.Value.Value)
                    Case EquivMatchMode.Between
                        If row.Value.HasValue Then query = query.Where(Function(r) r.Equivalents >= row.Value.Value)
                        If row.ValueTo.HasValue Then query = query.Where(Function(r) r.Equivalents <= row.ValueTo.Value)
                End Select
                Return New HashSet(Of String)(query.Select(Function(r) r.ProtocolItem.ExperimentID).Distinct())

            Case MaterialCategory.Solvent

                Dim query = SearchContext.tblSolvents.Where(Function(s) s.Name.ToUpper() = upperName)
                Select Case row.Mode
                    Case EquivMatchMode.EqualTo
                        If row.Value.HasValue Then query = query.Where(Function(s) s.Equivalents = row.Value.Value)
                    Case EquivMatchMode.AtLeast
                        If row.Value.HasValue Then query = query.Where(Function(s) s.Equivalents >= row.Value.Value)
                    Case EquivMatchMode.AtMost
                        If row.Value.HasValue Then query = query.Where(Function(s) s.Equivalents <= row.Value.Value)
                    Case EquivMatchMode.Between
                        If row.Value.HasValue Then query = query.Where(Function(s) s.Equivalents >= row.Value.Value)
                        If row.ValueTo.HasValue Then query = query.Where(Function(s) s.Equivalents <= row.ValueTo.Value)
                End Select
                Return New HashSet(Of String)(query.Select(Function(s) s.ProtocolItem.ExperimentID).Distinct())

            Case Else   ' MaterialCategory.Auxiliary

                Dim query = SearchContext.tblAuxiliaries.Where(Function(a) a.Name.ToUpper() = upperName)
                Select Case row.Mode
                    Case EquivMatchMode.EqualTo
                        If row.Value.HasValue Then query = query.Where(Function(a) a.Equivalents = row.Value.Value)
                    Case EquivMatchMode.AtLeast
                        If row.Value.HasValue Then query = query.Where(Function(a) a.Equivalents >= row.Value.Value)
                    Case EquivMatchMode.AtMost
                        If row.Value.HasValue Then query = query.Where(Function(a) a.Equivalents <= row.Value.Value)
                    Case EquivMatchMode.Between
                        If row.Value.HasValue Then query = query.Where(Function(a) a.Equivalents >= row.Value.Value)
                        If row.ValueTo.HasValue Then query = query.Where(Function(a) a.Equivalents <= row.ValueTo.Value)
                End Select
                Return New HashSet(Of String)(query.Select(Function(a) a.ProtocolItem.ExperimentID).Distinct())

        End Select

    End Function


    Private Sub lstResults_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstResults.PreviewMouseUp

        Dim selExp = TryCast(lstResults.SelectedItem, tblExperiments)

        If selExp IsNot Nothing Then
            Dim openArgs As New StepExpOpenArgs
            RaiseEvent RequestOpenExperiment(Me, selExp, chkServerSearch.IsChecked, openArgs)
        End If

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If e.Key = Key.Escape Then
            Me.Close()
        End If

    End Sub

End Class


''' <summary>
''' A single "material must be present" search filter: the material's category, its name, and an
''' optional equivalents range to additionally match. Mode/Value/ValueTo are edited via
''' dlgEquivalentsFilter (see dlgMaterialSearch.ChipBody_PreviewMouseUp), hence the change notification -
''' DisplayText recomputes and re-raises whenever any of them change, so the chip's bound label refreshes.
''' </summary>
'''
Public Class MaterialFilterRow

    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Property Category As MaterialCategory
    Public Property MaterialName As String

    Private _mode As EquivMatchMode
    Public Property Mode As EquivMatchMode
        Get
            Return _mode
        End Get
        Set(value As EquivMatchMode)
            If _mode <> value Then
                _mode = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Mode)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(DisplayText)))
            End If
        End Set
    End Property

    Private _value As Double?
    Public Property Value As Double?
        Get
            Return _value
        End Get
        Set(value As Double?)
            If Not Equals(_value, value) Then
                _value = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Me.Value)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(DisplayText)))
            End If
        End Set
    End Property

    Private _valueTo As Double?
    Public Property ValueTo As Double?
        Get
            Return _valueTo
        End Get
        Set(value As Double?)
            If Not Equals(_valueTo, value) Then
                _valueTo = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Me.ValueTo)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(DisplayText)))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Static summary shown on the chip, e.g. "≥1.5 eq Acetyl chloride" or "1-5 vq Water".
    ''' </summary>
    '''
    Public ReadOnly Property DisplayText As String
        Get
            Select Case Mode
                Case EquivMatchMode.AtLeast : Return $"{ChrW(8805)} {Value} {UnitLabel} {MaterialName}"
                Case EquivMatchMode.AtMost : Return $"{ChrW(8804)} {Value} {UnitLabel} {MaterialName}"
                Case EquivMatchMode.EqualTo : Return $"= {Value} {UnitLabel} {MaterialName}"
                Case EquivMatchMode.Between : Return $"{Value}-{ValueTo} {UnitLabel} {MaterialName}"
                Case Else : Return MaterialName
            End Select
        End Get
    End Property

    ''' <summary>
    ''' Chip background/border brushes matching this material's color in the protocol view (see
    ''' ReagentContent.xaml/SolventContent.xaml/AuxiliaryContent.xaml), softened for legibility as a
    ''' filled chip against the dialog's dark background rather than as plain text on white.
    ''' </summary>
    '''
    Public ReadOnly Property ChipBackground As Brush
        Get
            Select Case Category
                Case MaterialCategory.Reagent : Return ReagentChipBrush
                Case MaterialCategory.Solvent : Return SolventChipBrush
                Case Else : Return AuxiliaryChipBrush
            End Select
        End Get
    End Property

    Public ReadOnly Property ChipBorderBrush As Brush
        Get
            Select Case Category
                Case MaterialCategory.Reagent : Return ReagentChipBorderBrush
                Case MaterialCategory.Solvent : Return SolventChipBorderBrush
                Case Else : Return AuxiliaryChipBorderBrush
            End Select
        End Get
    End Property

    Private Shared ReadOnly ReagentChipBrush As Brush = FrozenBrush(90, 90, 90)
    Private Shared ReadOnly ReagentChipBorderBrush As Brush = FrozenBrush(175, 175, 175)

    Private Shared ReadOnly SolventChipBrush As Brush = FrozenBrush(47, 95, 204)
    Private Shared ReadOnly SolventChipBorderBrush As Brush = FrozenBrush(127, 168, 255)

    Private Shared ReadOnly AuxiliaryChipBrush As Brush = FrozenBrush(110, 66, 34)
    Private Shared ReadOnly AuxiliaryChipBorderBrush As Brush = FrozenBrush(196, 148, 100)

    Private Shared Function FrozenBrush(r As Byte, g As Byte, b As Byte) As Brush
        Dim brush As New SolidColorBrush(Color.FromRgb(r, g, b))
        brush.Freeze()
        Return brush
    End Function


    Public ReadOnly Property UnitLabel As String
        Get
            Select Case Category
                Case MaterialCategory.Reagent : Return "eq"
                Case MaterialCategory.Solvent : Return "vq"
                Case Else : Return "wq"
            End Select
        End Get
    End Property

End Class
