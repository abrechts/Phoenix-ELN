
Imports ElnCoreModel
Imports ElnBase.ELNEnumerations
Imports ElnBase.ELNCalculations
Imports System.Windows


Public Class dlgNewExperiment

    Public Enum ProjectSort
        SequenceNrDesc
        NameAscending
    End Enum


    Private _ProjectSortType As ProjectSort = ProjectSort.SequenceNrDesc
    Private _isInitializing As Boolean = False

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub Me_Loaded() Handles Me.Loaded

        _isInitializing = True

        chkSortProjects.IsChecked = (ProjectSortType = ProjectSort.NameAscending)
        SkipEmbeddedDocs = My.Settings.CloneSkipEmbedded
        CloneMethod = CType(My.Settings.LastCloneType, CloneType)

        ProjectSortType = My.Settings.NewExpProjectSort

        cboProjects.SelectedItem = TargetProject
        cboProjFolders.SelectedItem = TargetFolder

        cboScalingMatUnit.Text = My.Settings.LastScaleUnit

        _isInitializing = False

    End Sub


    ''' <summary>
    ''' Sets or gets the project in which to create the new experiment (also implies current project category)
    ''' </summary>
    Public Property TargetProject As tblProjects


    ''' <summary>
    ''' Sets or gets the project subfolder in which to create the new experiment
    ''' <returns></returns>
    ''' 
    Public Property TargetFolder As tblProjFolders

    ''' <summary>
    ''' Sets or gets the total grams amount of the source reference reactant (for scaling reference).
    ''' </summary>
    ''' 
    Public Property CurrentExperiment As tblExperiments


    ''' <summary>
    ''' Sets or gets scale factor between the target grams of reference reactant and the current source grams.
    ''' </summary>
    ''' 
    Public Property ScaleFactor As Double


    ''' <summary>
    ''' Sets or gets the clone method for the new experiment
    ''' </summary>
    ''' 
    Public Property CloneMethod As CloneType
        Get

            If rdoEmpty.IsChecked Then
                Return CloneType.EmptyExperiment

            ElseIf rdoSketch.IsChecked Then
                Return CloneType.SketchOnly

            ElseIf rdoNextStep.IsChecked Then
                Return CloneType.NextStepSketch

            ElseIf rdoClone.IsChecked Then
                Return CloneType.FullExperiment

            ElseIf rdoImport.IsChecked Then
                Return CloneType.FromImport

            Else
                Return CloneType.EmptyExperiment
            End If

        End Get

        Set(value As CloneType)

            Select Case value

                Case CloneType.EmptyExperiment
                    rdoEmpty.IsChecked = True

                Case CloneType.SketchOnly
                    rdoSketch.IsChecked = True

                Case CloneType.NextStepSketch
                    rdoNextStep.IsChecked = True

                Case CloneType.FullExperiment
                    rdoClone.IsChecked = True

                Case CloneType.FromImport
                    rdoImport.IsChecked = True

                Case Else
                    rdoEmpty.IsChecked = True

            End Select
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets if embedded documents should be excluded from the cloned experiment
    ''' </summary>
    ''' 
    Public Property SkipEmbeddedDocs As Boolean

        Get
            Return chkSkipDocs.IsChecked
        End Get

        Set(value As Boolean)
            chkSkipDocs.IsChecked = value
        End Set

    End Property


    Private Sub btnSortProjects_Click() Handles chkSortProjects.Checked, chkSortProjects.Unchecked

        ProjectSortType = If(chkSortProjects.IsChecked, ProjectSort.NameAscending, ProjectSort.SequenceNrDesc)

        If ProjectSortType = ProjectSort.SequenceNrDesc Then
            cboProjects.ItemsSource = From proj In CurrentExperiment.User.tblProjects Order By proj.SequenceNr Descending
        Else
            cboProjects.ItemsSource = From proj In CurrentExperiment.User.tblProjects Order By proj.Title.ToLower Ascending
        End If

    End Sub


    Private Property ProjectSortType As ProjectSort

        Get
            Return _ProjectSortType
        End Get

        Set(value As ProjectSort)

            _ProjectSortType = value

            chkSortProjects.IsChecked = (value = 1)
            If value = ProjectSort.SequenceNrDesc Then
                cboProjects.ItemsSource = From proj In CurrentExperiment.User.tblProjects Order By proj.SequenceNr Descending
            Else
                cboProjects.ItemsSource = From proj In CurrentExperiment.User.tblProjects Order By proj.Title.ToLower Ascending
            End If

        End Set

    End Property


    Private Sub cboProjects_SelectionChanged() Handles cboProjects.SelectionChanged

        TargetProject = cboProjects.SelectedItem

        If Not _isInitializing Then
            cboProjFolders.SelectedItem = TargetProject.tblProjFolders.First
        End If

    End Sub


    Private Sub cboProjFolders_SelectionChanged() Handles cboProjFolders.SelectionChanged

        TargetFolder = cboProjFolders.SelectedItem

    End Sub


    Private Sub chkCloneOptions_PreviewMouseDown() Handles pnlCloneOptions.PreviewMouseDown

        rdoClone.IsChecked = True

    End Sub


    Private Sub numMatAmount_IsVisibleChanged() Handles numMatAmount.IsVisibleChanged

        If chkScale.IsChecked Then
            numMatAmount.Focus()
        End If

    End Sub


    Private Sub btnOK_Click(sender As Object, e As RoutedEventArgs) Handles btnOK.Click

        If CloneMethod = CloneType.FullExperiment AndAlso chkScale.IsChecked Then

            If numMatAmount.IsZeroOrNothing Then
                MsgBox("Please enter a valid scaling amount!", MsgBoxStyle.Information, "Validation")
                numMatAmount.Focus()
                e.Handled = True
                Exit Sub
            End If

            If GetMaterialUnitType(cboScalingMatUnit.Text) = MaterialUnitType.Weight Then
                Dim destinationGrams = ConvertToGrams(numMatAmount.Value, GetEnumVal(Of WeightUnit)(cboScalingMatUnit.Text))
                ScaleFactor = destinationGrams / CurrentExperiment.RefReactantGrams
            Else
                Dim destinationMMols = ConvertToMMol(numMatAmount.Value, GetEnumVal(Of MolUnit)(cboScalingMatUnit.Text))
                ScaleFactor = destinationMMols / CurrentExperiment.RefReactantMMols
            End If

        Else

            ScaleFactor = 1

        End If

        My.Settings.LastCloneType = CloneMethod
        My.Settings.CloneSkipEmbedded = SkipEmbeddedDocs
        My.Settings.NewExpProjectSort = If(chkSortProjects.IsChecked, 1, 0)
        My.Settings.LastScaleUnit = cboScalingMatUnit.Text

        e.Handled = True
        Me.DialogResult = True
        Me.Close()

    End Sub

    Private Sub btnCancel_Click(sender As Object, e As RoutedEventArgs) Handles btnCancel.Click

        Me.DialogResult = False
        Me.Close()

    End Sub


End Class

