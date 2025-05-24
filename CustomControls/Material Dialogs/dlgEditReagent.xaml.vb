Imports System.Windows.Input
Imports ElnBase
Imports ElnBase.ELNCalculations
Imports ElnBase.ELNEnumerations
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore
Imports System.Windows
Imports Key = System.Windows.Input.Key
Imports System.Collections.ObjectModel
Imports System.Windows.Controls

Public Class dlgEditReagent

    Public Sub New()

        'This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets the data bound source material entity.
    ''' </summary>
    ''' 
    Public Property ReagentEntry As tblReagents


    ''' <summary>
    ''' Sets or gets the materials DB entry corresponding to the current ReagentEntry. For new 
    ''' or unknown materials, this entry contains db infrastructure without mat properties.
    ''' </summary>
    '''
    Private Property MatDbEntry As tblMaterials


    ''' <summary>
    ''' Sets or gets if a new material is being added.
    ''' </summary>
    ''' 
    Public Property IsAddingNew As Boolean


    Public Sub Me_Loaded() Handles Me.Loaded

        blkTitle.Text = If(IsAddingNew, "Add Reagent", "Edit Reagent")

        cboSearch.QueryItemsSource = (From mat In ProtocolItemBase.DbInfo.tblMaterials
                                      Where mat.MatType = MaterialType.Reagent
                                      Order By mat.MatName.ToLower).ToList

        If IsAddingNew Then

            MatDbEntry = ProtocolItemBase.CreateNewMatDBEntry(MaterialType.Reagent)

            cboMwMolarity.SelectedIndex = 0
            cboMatUnit.Text = My.Settings.LastReagentUnit
            ReagentEntry.SpecifiedUnitType = GetMaterialUnitType(cboMatUnit.Text)

        Else

            PopulateData()
            MatDbEntry = GetMatchingDbReagent()
            If MatDbEntry Is Nothing Then
                'rare case where material was not stored in matDB for some reason
                MatDbEntry = ProtocolItemBase.CreateNewMatDBEntry(MaterialType.Reagent)
            End If

        End If

        SetValidationLockState(MatDbEntry)     'handles the 'validated' label visibility
        matDbDocsCtrl.Documents = New ObservableCollection(Of tblDbMaterialFiles)(MatDbEntry.tblDbMaterialFiles)

        numMatAmount.Focus()
        numMatAmount.Select(255, 0)

    End Sub


    ''' <summary>
    ''' Gets the materials database entry matching the name of the current reagent. Returns nothing if not present.
    ''' </summary>
    ''' 
    Private Function GetMatchingDbReagent() As tblMaterials

        Dim reagentHit = (From mat In ProtocolItemBase.DbInfo.tblMaterials Where mat.MatType = MaterialType.Reagent _
                          AndAlso mat.MatName.Equals(ReagentEntry.Name, StringComparison.CurrentCultureIgnoreCase)).FirstOrDefault

        Return reagentHit

    End Function


    Private Sub SetValidationLockState(matHit As tblMaterials)

        If matHit Is Nothing OrElse matHit.IsValidated Is Nothing OrElse matHit.IsValidated = MaterialValidation.None Then

            pnlValidated.Visibility = Visibility.Hidden
            numMwMolar.IsEnabled = True
            numDensity.IsEnabled = True
            cboMwMolarity.IsEnabled = True

        Else

            pnlValidated.Visibility = Visibility.Visible
            numMwMolar.IsEnabled = False
            numDensity.IsEnabled = False
            cboMwMolarity.IsEnabled = False

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
                    icoInfo.Visibility = Visibility.Visible
                    blkUnitInfo.Text = info(0)
                    icoInfo.ToolTip = info(1)
                Else
                    blkUnitInfo.Text = ""
                    icoInfo.Visibility = Visibility.Collapsed
                End If
            End If
        End With

    End Sub


    Private Sub cboMwMolarity_SelectionChange() Handles cboMwMolarity.SelectionChanged

        With cboMwMolarity

            If .IsMouseOver Then
                'user-initiated selection
                If cboMatUnit.SelectedIndex > 1 Then
                    cboMatUnit.Text = If(.SelectedIndex = 0, WeightUnit.g.ToString, VolumeUnit.mL.ToString)
                Else
                    'equiv unit selected
                    chkConvertVolWeight.IsChecked = False
                End If
                numMwMolar.Text = ""
            End If

            If .SelectedIndex = 0 Then
                chkConvertVolWeight.Content = "Display As Volume"
                pnlResinLoad.Visibility = Visibility.Visible
                sepResinLoad.Visibility = Visibility.Visible
            Else
                chkConvertVolWeight.Content = "Display As Weight"
                pnlResinLoad.Visibility = Visibility.Collapsed
                sepResinLoad.Visibility = Visibility.Collapsed
                numResinLoad.Value = Nothing
            End If

        End With

    End Sub


    Private Sub cboSearch_MaterialSelected(sender As Object, selItem As tblMaterials) Handles cboSearch.MaterialSelected

        ClearMatProperties()

        If selItem IsNot Nothing Then

            PopulateMatProperties(selItem)

            MatDbEntry = selItem
            matDbDocsCtrl.Documents = New ObservableCollection(Of tblDbMaterialFiles)(MatDbEntry.tblDbMaterialFiles)

            If Keyboard.Modifiers And ModifierKeys.Control = ModifierKeys.Control Then
                If Me.IsVisible Then
                    btnOK_Click()  'shortcut for immediately committing database entry and closing dialog
                End If
            End If

        Else

            MatDbEntry = ProtocolItemBase.CreateNewMatDBEntry(MaterialType.Reagent)
            matDbDocsCtrl.Documents = New ObservableCollection(Of tblDbMaterialFiles)(MatDbEntry.tblDbMaterialFiles)

        End If

    End Sub


    Private Sub cboSearch_RequestDeleteItem(sender As Object, item As tblMaterials) Handles cboSearch.RequestDeleteItem

        ProtocolItemBase.DbInfo.tblMaterials.Remove(item)
        ClearMatProperties()

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


    Private Sub PopulateMatProperties(materialEntry As tblMaterials)

        If materialEntry IsNot Nothing Then

            With materialEntry
                cboSearch.Text = .MatName
                txtSupplier.Text = .MatSource
                numMwMolar.Value = If(.Molarity Is Nothing, Format(.Molweight, "0.00"), Format(.Molarity, "0.00"))
                cboMwMolarity.SelectedIndex = If(.Molarity IsNot Nothing, 1, 0)
                numDensity.Text = If(Format(.Density, "0.00"), "")
                txtSupplier.Text = .MatSource
                numPurity.Text = If(Format(.Purity, "0.0"), "")
            End With

        End If

        SetValidationLockState(materialEntry)

    End Sub


    Private Sub ClearMatProperties()

        txtSupplier.Text = ""
        numMwMolar.Value = Nothing
        cboMwMolarity.SelectedIndex = 0
        numDensity.Text = Nothing
        txtSupplier.Text = ""
        numPurity.Text = ""
        numResinLoad.Text = ""

        matDbDocsCtrl.Documents.Clear()

        SetValidationLockState(Nothing)

    End Sub


    Private Sub PopulateData()

        'populates the UI from code 

        With ReagentEntry

            cboSearch.Text = .Name
            txtSupplier.Text = .Source
            numMwMolar.Value = If(.IsMolarity, .Molarity, .MolecularWeight)
            cboMwMolarity.SelectedIndex = If(.IsMolarity, 1, 0)
            chkConvertVolWeight.IsChecked = .IsDisplayAsVolume

            numResinLoad.Value = .ResinLoad
            numDensity.Text = If(.Density, "")
            numPurity.Text = If(.Purity, "")

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Equivalent

                    Dim scaled = ELNCalculations.ScaleEquivalent(.Equivalents, isShortUnit:=True)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

                Case MaterialUnitType.Weight

                    Dim scaled = ELNCalculations.ScaleWeight(.Grams)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

                Case MaterialUnitType.Volume

                    'molar solution
                    Dim scaled = ELNCalculations.ScaleVolume(.Grams)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

            End Select

        End With

    End Sub


    Private Function ValidateData() As Boolean

        If numMatAmount.IsZeroOrNothing Then
            MsgBox("Please specify a material amount.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMatAmount.Focus()
            Return False

        ElseIf Trim(cboSearch.Text) = "" Then
            MsgBox("Please specify a material name.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            cboSearch.ActivateEdit()
            Return False

        ElseIf numMwMolar.IsZeroOrNothing Then
            MsgBox("Please specify a molecular weight or a molarity.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMwMolar.Focus()
            Return False

        ElseIf cboMwMolarity.SelectedIndex = 1 AndAlso numMwMolar.Value > 20 Then
            MsgBox("The molarity can't be higher than 20.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMwMolar.Focus()
            Return False

        ElseIf numDensity.Value = 0 OrElse numDensity.Value > 16 Then
            MsgBox("The density must be in the range between 1 to 16.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numDensity.Focus()
            Return False

        ElseIf numDensity.IsZeroOrNothing AndAlso chkConvertVolWeight.IsChecked Then
            MsgBox("A density is required for the desired " + vbCrLf +
                   "weight/volume conversion.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numDensity.Focus()
            Return False

        ElseIf numPurity.Value = 0 OrElse numPurity.Value > 100 Then
            MsgBox("The purity must be in the range between 1 to 100%.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numPurity.Focus()
            Return False

        ElseIf numResinLoad.Value = 0 Then
            MsgBox("The resin load can't be zero. Leave the field" + vbCrLf +
                   "empty or enter a valid value", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numResinLoad.Focus()
            Return False

        End If

        Return True

    End Function


    Private Sub CommitData()

        'commits the UI values to ReagentEntry 

        With ReagentEntry

            .Name = cboSearch.Text
            .Source = txtSupplier.Text
            .Density = numDensity.Value
            .IsDisplayAsVolume = chkConvertVolWeight.IsChecked
            .Purity = numPurity.Value
            .ResinLoad = numResinLoad.Value

            If cboMwMolarity.SelectedIndex = 1 Then
                .IsMolarity = True
                .Molarity = numMwMolar.Value
                .MolecularWeight = Nothing
            Else
                .IsMolarity = False
                .MolecularWeight = numMwMolar.Value
                .Molarity = Nothing
            End If

            .SpecifiedUnitType = GetMaterialUnitType(cboMatUnit.Text)

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Equivalent
                    Dim shortEquivUnit = GetEnumVal(Of EquivUnitShort)(cboMatUnit.Text)
                    .Equivalents = ConvertToEquiv(numMatAmount.Value, shortEquivUnit)

                Case MaterialUnitType.Weight
                    Dim weightUnit = GetEnumVal(Of WeightUnit)(cboMatUnit.Text)
                    .Grams = ConvertToGrams(numMatAmount.Value, weightUnit)

                Case MaterialUnitType.Volume    'molar solution specified as volume
                    Dim volUnit = GetEnumVal(Of VolumeUnit)(cboMatUnit.Text)
                    .Grams = ConvertToML(numMatAmount.Value, volUnit)

            End Select

            My.Settings.LastReagentUnit = cboMatUnit.Text

        End With

        MatDbEntry.CurrDocIndex = matDbDocsCtrl.SelectedDocIndex

        RecalculateReagent(ReagentEntry)

    End Sub


    Private Sub btnOK_Click() Handles btnOk.Click

        If ValidateData() Then

            CommitData()

            'add/update materials database
            With ReagentEntry
                ProtocolItemBase.UpdateMaterialsDB(MatDbEntry, matDbDocsCtrl.Documents.ToList, MaterialType.Reagent, .Name,
                   .Source, .Density, .Purity, .MolecularWeight, .Molarity)
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
