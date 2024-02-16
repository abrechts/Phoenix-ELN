Imports System.Windows
Imports ElnBase
Imports ElnBase.ELNCalculations
Imports ElnCoreModel


Public Class dlgEditProduct

    Public Sub New()

        'This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Sub Me_Loaded() Handles Me.Loaded

        'create new product
        If SketchInfo.Products.Count = 1 Then
            rdoProdB.Visibility = Visibility.Collapsed
            rdoProdC.Visibility = Visibility.Collapsed
        ElseIf SketchInfo.Products.Count = 2 Then
            rdoProdC.Visibility = Visibility.Collapsed
        End If

        pnlResinLoad.Visibility = If(SketchInfo.Products(ProductEntry.ProductIndex).IsAttachedToResin, Visibility.Visible, Visibility.Collapsed)

        blkTitle.Text = If(IsAddingNew AndAlso Not IsFromPlaceholder, "Add Product", "Edit Product")
        cboMatUnit.Text = My.Settings.LastProductUnit

        If Not IsAddingNew Then
            PopulateData()
            pnlProdSelection.IsEnabled = False
        ElseIf IsFromPlaceholder Then
            SelectProductButton(ProductEntry.ProductIndex)
        Else
            rdoProdA_Checked()
        End If

        If numResinLoad.IsVisible AndAlso ProductEntry.ResinLoad Is Nothing Then
            numResinLoad.Focus()
            numMatAmount.Select(255, 0)
        Else
            numMatAmount.Focus()
            numMatAmount.Select(255, 0)
        End If

    End Sub


    ''' <summary>
    ''' Sets or gets the original, data bound entity.
    ''' </summary>
    ''' 
    Public Property ProductEntry As tblProducts


    ''' <summary>
    ''' Sets or gets if a new material is being added.
    ''' </summary>
    ''' 
    Public Property IsAddingNew As Boolean


    ''' <summary>
    ''' Sets or gets if the dialog was opened from a product placeholder
    ''' </summary>
    '''
    Public Property IsFromPlaceholder As Boolean = False


    ''' <summary>
    ''' Sets or gets the properties of the current reaction sketch
    ''' </summary>
    ''' 
    Public Property SketchInfo As SketchResults


    Private Sub rdoProdA_Checked() Handles rdoProdA.Checked

        If SketchInfo IsNot Nothing Then

            ProductEntry.ProductIndex = 0
            pnlResinLoad.Visibility = If(SketchInfo.Products(0).IsAttachedToResin, Visibility.Visible, Visibility.Collapsed)
            blkMW.Text = SketchInfo.Products(0).Molweight.ToString("0.00")
            txtMatName.Text = "Product"

        End If

    End Sub

    Private Sub rdoProdB_Checked() Handles rdoProdB.Checked

        ProductEntry.ProductIndex = 1
        pnlResinLoad.Visibility = If(SketchInfo.Products(1).IsAttachedToResin, Visibility.Visible, Visibility.Collapsed)
        blkMW.Text = SketchInfo.Products(1).Molweight.ToString("0.00")
        txtMatName.Text = "Side Prod 1"

    End Sub

    Private Sub rdoProdC_Checked() Handles rdoProdC.Checked

        ProductEntry.ProductIndex = 2
        pnlResinLoad.Visibility = If(SketchInfo.Products(2).IsAttachedToResin, Visibility.Visible, Visibility.Collapsed)
        blkMW.Text = SketchInfo.Products(2).Molweight.ToString("0.00")
        txtMatName.Text = "Side Prod 2"

    End Sub


    Private Sub PopulateData()

        'populates the UI from code 

        With ProductEntry

            txtMatName.Text = .Name
            numPurity.Text = If(.Purity, "")
            numResinLoad.Value = .ResinLoad
            SelectProductButton(.ProductIndex)

            Dim scaled = ELNCalculations.ScaleWeight(.Grams)
            numMatAmount.Text = SignificantDigitsString(scaled.Amount, 3)
            cboMatUnit.Text = scaled.Unit

        End With

    End Sub


    Private Sub SelectProductButton(index As Integer)

        Select Case index
            Case 0
                rdoProdA.IsChecked = True
            Case 1
                rdoProdB.IsChecked = True
            Case Else
                rdoProdC.IsChecked = True
        End Select

    End Sub


    Private Function ValidateData() As Boolean

        If numMatAmount.IsZeroOrNothing Then
            MsgBox("Please specify a material amount.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numMatAmount.Focus()
            Return False

        ElseIf Trim(txtMatName.Text) = "" Then
            MsgBox("Please specify a material name.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            txtMatName.Focus()
            Return False

        ElseIf numPurity.Value = 0 OrElse numPurity.Value > 100 Then
            MsgBox("The purity must be in the range between 1 to 100%.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numPurity.Focus()
            Return False

        ElseIf numResinLoad.IsVisible AndAlso val(numResinLoad.Text) = 0 Then
            MsgBox("Please specify a resin load.", MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Data Validation")
            numResinLoad.Focus()
            Return False

        End If

        Return True

    End Function


    Private Sub CommitData()

        With ProductEntry
            .Grams = ConvertToGrams(numMatAmount.Value, ToWeightUnit(cboMatUnit.Text))
            .Name = txtMatName.Text
            .Purity = numPurity.Value
            .MolecularWeight = SketchInfo.Products(.ProductIndex).Molweight
            .ExactMass = SketchInfo.Products(.ProductIndex).ExactMass
            .ElementalFormula = SketchInfo.Products(.ProductIndex).EFString
            .InChIKey = SketchInfo.Products(.ProductIndex).InChIKey
            .ResinLoad = numResinLoad.Value
        End With

        My.Settings.LastProductUnit = cboMatUnit.Text

        RecalculateProduct(ProductEntry)

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
        Me.Close()

    End Sub


End Class
