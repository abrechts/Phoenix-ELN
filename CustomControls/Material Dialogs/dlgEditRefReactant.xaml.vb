Imports ElnBase
Imports ElnBase.ELNEnumerations
Imports ElnBase.ELNCalculations
Imports ElnCoreModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Public Class dlgEditRefReactant

    Public Sub New()

        'This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Sets or gets the reaction sketch information.
    ''' </summary>
    ''' 
    Public Property SketchInfo As SketchResults


    ''' <summary>
    ''' Sets or gets the original, data bound entity.
    ''' </summary>
    ''' 
    Public Property ReferenceReactant As tblRefReactants


    ''' <summary>
    ''' Sets or gets if a new material is being added.
    ''' </summary>
    ''' 
    Public Property IsAddingNew As Boolean


    ''' <summary>
    ''' Sets or gets if the original reference reactant amount was modified, requiring protocol recalculation
    ''' </summary>
    ''' 
    Public Property IsRecalcRequired As Boolean



    Public Sub Me_Loaded() Handles Me.Loaded

        blkMW.Text = SketchInfo.Reactants.First.Molweight.ToString("0.00")
        blkTitle.Text = If(IsAddingNew, "Add Ref. Reactant", "Edit Ref. Reactant")
        pnlResinLoad.Visibility = If(SketchInfo.Reactants.First.IsAttachedToResin, Visibility.Visible, Visibility.Collapsed)

        If IsAddingNew Then

            With ReferenceReactant
                cboMatUnit.Text = My.Settings.LastRefReactUnit
                txtMatName.Text = .Name
                txtSupplier.Text = .Source
                numDensity.Value = .Density
                numPurity.Value = .Purity
                numResinLoad.Value = .ResinLoad
                chkConvertVolWeight.IsChecked = .IsDisplayAsVolume
            End With

        Else

            PopulateData()

        End If

        If numResinLoad.IsVisible AndAlso ReferenceReactant.ResinLoad Is Nothing Then
            numResinLoad.Focus()
            numResinLoad.Select(255, 0)
        Else
            numMatAmount.Focus()
            numMatAmount.Select(255, 0)
        End If


    End Sub


    ''' <summary>
    ''' Commit NumericTextbox value when Enter or Return key is pressed
    ''' </summary>
    '''
    Private Sub Me_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles Me.PreviewKeyDown

        If TypeOf e.OriginalSource Is NumericTextBox AndAlso (e.Key = Key.Return OrElse e.Key = Key.Enter) Then

            Me.Focus()  'move focus to window (-> causes sender LostFocus event)
            If Not Validation.GetHasError(e.OriginalSource) Then
                Keyboard.ClearFocus()  'clear textbox cursor
            End If

            e.Handled = True    'important to prevent insertion of new line in active comment item

        End If

    End Sub


    Private Sub PopulateData()

        'populates the UI from code 

        With ReferenceReactant

            txtMatName.Text = .Name
            txtSupplier.Text = .Source
            chkConvertVolWeight.IsChecked = .IsDisplayAsVolume

            numDensity.Text = If(.Density, "")
            numPurity.Text = If(.Purity, "")
            numResinLoad.Value = ReferenceReactant.ResinLoad

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Weight

                    Dim scaled = ELNCalculations.ScaleWeight(.Grams)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

                Case MaterialUnitType.Mol

                    Dim scaled = ELNCalculations.ScaleMMol(.MMols)
                    numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
                    cboMatUnit.Text = scaled.Unit

            End Select

        End With

    End Sub


    Private Function ValidateData() As Boolean

        If numMatAmount.IsZeroOrNothing Then
            cbMsgBox.Display("Please specify a material amount.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMatAmount.Focus()
            Return False

        ElseIf Trim(txtMatName.Text) = "" Then
            cbMsgBox.Display("Please specify a material name.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            txtMatName.Focus()
            Return False

        ElseIf SketchInfo.Reactants.First.IsAttachedToResin AndAlso Val(numResinLoad.Text) = 0 Then
            cbMsgBox.Display("Please specify a valid resin load.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numResinLoad.Focus()
            Return False

        ElseIf numDensity.Value = 0 OrElse numDensity.Value > 16 Then
            cbMsgBox.Display("The density must be in the range between 1 to 16.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numDensity.Focus()
            Return False

        ElseIf numDensity.IsZeroOrNothing AndAlso chkConvertVolWeight.IsChecked Then
            cbMsgBox.Display("A density is required for the desired " + vbCrLf +
                   "weight/volume conversion.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numDensity.Focus()
            Return False

        ElseIf numPurity.Value = 0 OrElse numPurity.Value > 100 Then
            cbMsgBox.Display("The purity must be in the range between 1 to 100%.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numPurity.Focus()
            Return False

        End If

        Return True

    End Function


    Private Sub CommitData()

        'commits the UI values to ReagentEntry 

        With ReferenceReactant

            Dim prevGrams = .Grams
            Dim prevMMols = .MMols
            Dim prevPurity = .Purity
            Dim prevResinLoad = .ResinLoad

            .Name = txtMatName.Text
            .Source = txtSupplier.Text
            .IsDisplayAsVolume = chkConvertVolWeight.IsChecked
            .Density = numDensity.Value
            .Purity = numPurity.Value
            .MolecularWeight = SketchInfo.Reactants.First.Molweight
            .InChIKey = SketchInfo.Reactants.First.InChIKey
            .ResinLoad = numResinLoad.Value

            .SpecifiedUnitType = GetMaterialUnitType(cboMatUnit.Text)

            Select Case .SpecifiedUnitType

                Case MaterialUnitType.Weight
                    Dim weightUnit = GetEnumVal(Of WeightUnit)(cboMatUnit.Text)
                    .Grams = ConvertToGrams(numMatAmount.Value, weightUnit)

                Case MaterialUnitType.Mol
                    Dim molUnit = GetEnumVal(Of MolUnit)(cboMatUnit.Text)
                    .MMols = ConvertToMMol(numMatAmount.Value, molUnit)

            End Select

            IsRecalcRequired = (prevGrams <> .Grams OrElse
                                .MMols <> prevMMols OrElse
                                 Not prevPurity.Equals(.Purity) OrElse
                                 Not prevResinLoad.Equals(.ResinLoad))

            My.Settings.LastRefReactUnit = cboMatUnit.Text

        End With

        RecalculateRefReactant(ReferenceReactant)

    End Sub


    Private Sub btnOK_Click() Handles btnOk.Click

        If ValidateData() Then

            CommitData()

            DialogResult = True
            Me.Close()

        End If

    End Sub


    Private Sub btnCancel_Click() Handles btnCancel.Click

        DialogResult = False
        Me.Close()  'temporary clone is discarded

    End Sub


End Class
