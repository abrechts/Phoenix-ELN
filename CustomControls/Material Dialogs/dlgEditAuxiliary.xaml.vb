Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnBase.ELNCalculations
Imports ElnCoreModel
Imports System.Windows.Input
Imports System.Windows

Public Class dlgEditAuxiliary


    Public Sub New()

        'This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets the data bound source material entity.
    ''' </summary>
    ''' 
    Public Property AuxiliaryEntry As tblAuxiliaries


    ''' <summary>
    ''' Sets or gets if a new material is being added.
    ''' </summary>
    ''' 
    Public Property IsAddingNew As Boolean


    Public Sub Me_Loaded() Handles Me.Loaded

        blkTitle.Text = If(IsAddingNew, "Add Auxiliary", "Edit Auxiliary")

        cboSearch.QueryItemsSource = (From mat In ProtocolItemBase.DbInfo.tblMaterials
                                      Where mat.MatType = MaterialType.Auxiliary
                                      Order By mat.MatName.ToLower).ToList

        If IsAddingNew Then
            cboMatUnit.Text = My.Settings.LastAuxiliaryUnit
            AuxiliaryEntry.SpecifiedUnitType = GetMaterialUnitType(cboMatUnit.Text)
        Else
            PopulateData()
            SetValidationInfo()
        End If

        numMatAmount.Focus()
        numMatAmount.Select(255, 0)

    End Sub


    ''' <summary>
    ''' Determines if the current material corresponds to a validated material in the materials list 
    ''' and updates the dialog UI accordingly.
    ''' </summary>
    ''' 
    Private Sub SetValidationInfo()

        With AuxiliaryEntry

            Dim matHit = (From mat In ProtocolItemBase.DbInfo.tblMaterials Where mat.MatType = MaterialType.Auxiliary _
                           AndAlso mat.MatName.Equals(.Name, StringComparison.CurrentCultureIgnoreCase) _
                           AndAlso mat.Density.Equals(.Density)).FirstOrDefault

            SetValidationLockState(matHit)

        End With

    End Sub


    Private Sub SetValidationLockState(matHit As tblMaterials)

        If matHit Is Nothing OrElse matHit.IsValidated = MaterialValidation.None Then

            pnlValidated.Visibility = Visibility.Hidden
            numDensity.IsEnabled = True

        Else

            pnlValidated.Visibility = Visibility.Visible
            numDensity.IsEnabled = False
            If matHit.IsValidated = MaterialValidation.Preset Then
                blkValidatedTitle.Text = "Preset"
            ElseIf matHit.IsValidated = MaterialValidation.User Then
                'not yet implemented ...
                blkValidatedTitle.Text = "Validated"
            End If

        End If

    End Sub



    Private Sub cboMatUnit_SelectionChanged() Handles cboMatUnit.SelectionChanged

        With cboMatUnit
            If .IsInitialized Then
                If .SelectedItem.Tag <> "" Then
                    Dim info = .SelectedItem.Tag.split("/")
                    icoInfo.Visibility = Windows.Visibility.Visible
                    blkUnitInfo.Text = info(0)
                    icoInfo.ToolTip = info(1)
                Else
                    blkUnitInfo.Text = ""
                    icoInfo.Visibility = Windows.Visibility.Collapsed
                End If
            End If
        End With

    End Sub


    Private Sub cboSearch_MaterialSelected(sender As Object, selItem As tblMaterials) Handles cboSearch.MaterialSelected

        If selItem IsNot Nothing Then

            PopulateMatProperties(selItem)
            If Keyboard.Modifiers And ModifierKeys.Control = ModifierKeys.Control Then
                btnOK_Click()  'shortcut for immediately committing database entry and closing dialog
            End If

        Else

            ClearMatProperties()

        End If

    End Sub


    Private Sub cboSearch_RequestDeleteItem(sender As Object, item As tblMaterials) Handles cboSearch.RequestDeleteItem

        ProtocolItemBase.DbInfo.tblMaterials.Remove(item)
        ClearMatProperties()

    End Sub


    Private Sub PopulateMatProperties(materialEntry As tblMaterials)

        If materialEntry IsNot Nothing Then

            With materialEntry

                cboSearch.Text = .MatName
                txtSupplier.Text = .MatSource
                numDensity.Text = If(Format(.Density, "0.00"), "")

            End With

        End If

        SetValidationLockState(materialEntry)

    End Sub


    Private Sub ClearMatProperties()

        txtSupplier.Text = ""
        numDensity.Text = Nothing
        txtSupplier.Text = ""

        SetValidationLockState(Nothing)

    End Sub


    Private Sub PopulateData()

        'populates the UI from code 

        With AuxiliaryEntry

            cboSearch.Text = .Name
            txtSupplier.Text = .Source
            chkConvertVolWeight.IsChecked = .IsDisplayAsVolume
            numDensity.Text = If(.Density, "")

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Equivalent

                    numMatAmount.Text = SignificantDigitsString(.Equivalents, 3)
                    cboMatUnit.Text = EquivUnitShort.wq.ToString

                Case MaterialUnitType.Weight

                    Dim scaled = ELNCalculations.ScaleWeight(.Grams)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

            End Select

        End With

    End Sub


    Private Function ValidateData()

        If numMatAmount.IsZeroOrNothing Then
            MsgBox("Please specify a material amount!", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMatAmount.Focus()
            Return False

        ElseIf Trim(cboSearch.Text) = "" Then
            MsgBox("Please specify a material name!", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            cboSearch.ActivateEdit()
            Return False

        ElseIf numDensity.IsZeroOrNothing AndAlso chkConvertVolWeight.IsChecked Then
            MsgBox("A density is required for the desired " + vbCrLf +
                   "weight/volume conversion.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Required")
            numDensity.Focus()
            Return False

        End If

        Return True

    End Function


    Private Sub CommitData()

        'commits the UI values to auxiliary entry 

        With AuxiliaryEntry

            .Name = cboSearch.Text
            .Source = txtSupplier.Text
            .Density = numDensity.Value
            .IsDisplayAsVolume = chkConvertVolWeight.IsChecked
            .SpecifiedUnitType = GetMaterialUnitType(cboMatUnit.Text)

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Equivalent
                    Dim equivUnit = GetEnumVal(Of EquivUnitShort)(cboMatUnit.Text)
                    .Equivalents = ConvertToEquiv(numMatAmount.Value, equivUnit)

                Case MaterialUnitType.Weight    'molar solution
                    Dim weightUnit = GetEnumVal(Of WeightUnit)(cboMatUnit.Text)
                    .Grams = ConvertToGrams(numMatAmount.Value, weightUnit)

            End Select

            My.Settings.LastAuxiliaryUnit = cboMatUnit.Text

        End With

        RecalculateAuxiliary(AuxiliaryEntry)

    End Sub


    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If Not cboSearch.IsDropDownOpen Then
            If e.Key = Key.Return OrElse e.Key = Key.Enter Then
                btnOK_Click()
                e.Handled = True    'important to prevent insertion of new line in active comment item
            ElseIf e.Key = Key.Escape Then
                btnCancel_Click()
            End If
        End If

    End Sub


    Private Sub btnOK_Click() Handles btnOk.Click

        If ValidateData() Then

            CommitData()

            'add/update materials database
            With AuxiliaryEntry
                ProtocolItemBase.UpdateMaterialsDB(MaterialType.Auxiliary, .Name, .Source, .Density, Nothing, Nothing, Nothing)
            End With

            DialogResult = True
            Me.Close()

        End If

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False
        Me.Close()

    End Sub


End Class
