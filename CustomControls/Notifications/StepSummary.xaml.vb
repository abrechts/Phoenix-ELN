Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports ElnCoreModel

Public Class StepSummary

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        cvsStepExperiments = Me.TryFindResource("cvsStepExperiments")

        AddHandler SketchArea.SketchSourceChanged, AddressOf SketchArea_SketchSourceChanged

    End Sub


    Private Property cvsStepExperiments As CollectionViewSource

    Private Property RefReactInChIKey As String

    Private Property RefProductInChIKey As String


    Private Sub Me_DataContextChanged() Handles Me.DataContextChanged

        If Me.DataContext IsNot Nothing Then
            cvsStepExperiments.Source = CType(Me.DataContext, tblUsers).tblExperiments
            cboSortType_Changed()
        End If

    End Sub


    Private Sub SketchArea_SketchSourceChanged(sender As Object, skInfo As SketchResults) 'shared event

        If skInfo IsNot Nothing AndAlso skInfo.Reactants.Count > 0 AndAlso skInfo.Products.Count > 0 Then

            RefReactInChIKey = skInfo.Reactants.First.InChIKey
            RefProductInChIKey = skInfo.Products.First.InChIKey
            cvsStepExperiments.View.Refresh()

            pnlReactLinks.IsEnabled = True
            pnlProdLinks.IsEnabled = True
            pnlReactLinks.Opacity = 1
            pnlProdLinks.Opacity = 1

            With skInfo.Reactants.First
                blkInChIKeyReact.Text = .InChIKey
                blkReactEf.Text = .EFString
                blkReactMw.Text = Format(.Molweight, "0.00")
                blkReactEM.Text = Format(.ExactMass, "0.00")
            End With

            With skInfo.Products.First
                blkInChIKeyProd.Text = .InChIKey
                blkProdEf.Text = .EFString
                blkProdMw.Text = Format(.Molweight, "0.00")
                blkProdEM.Text = Format(.ExactMass, "0.00")
            End With

        Else

            pnlReactLinks.IsEnabled = False
            pnlProdLinks.IsEnabled = False
            pnlReactLinks.Opacity = 0.5
            pnlProdLinks.Opacity = 0.5

            RefReactInChIKey = " --- "
            RefProductInChIKey = " --- "
            cvsStepExperiments.View.Refresh()

            blkInChIKeyReact.Text = " --- "
            blkReactEf.Text = " --- "
            blkReactMw.Text = " --- "
            blkReactEM.Text = " --- "

            blkInChIKeyProd.Text = " --- "
            blkProdEf.Text = " --- "
            blkProdMw.Text = " --- "
            blkProdEM.Text = " --- "

        End If

    End Sub


    Private Sub lnkPubChemReact_Click() Handles lnkPubChemReact.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://pubchem.ncbi.nlm.nih.gov/compound/" + blkInChIKeyReact.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub lnkChemSpiderReact_Click() Handles lnkChemSpiderReact.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://www.chemspider.com/Search.aspx?q=" + blkInChIKeyReact.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub lnkPubChemProd_Click() Handles lnkPubChemProd.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://pubchem.ncbi.nlm.nih.gov/compound/" + blkInChIKeyProd.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub lnkChemSpiderProd_Click() Handles lnkChemSpiderProd.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://www.chemspider.com/Search.aspx?q=" + blkInChIKeyProd.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub





    Private Sub cvsStepExperiments_Filter(sender As Object, e As FilterEventArgs) 'XAML event

        Dim expEntry As tblExperiments = e.Item

        If expEntry.ReactantInChIKey = RefReactInChIKey AndAlso expEntry.ProductInChIKey = RefProductInChIKey Then
            e.Accepted = True
        Else
            e.Accepted = False
        End If

    End Sub


    Private Sub ListBoxItem_Selected(sender As Object, e As MouseButtonEventArgs) Handles lstStepExperiments.PreviewMouseUp

        Dim selExp = lstStepExperiments.SelectedItem

        If selExp IsNot Nothing Then
            lstStepExperiments.UnselectAll()
            RaiseEvent RequestOpenExperiment(Me, selExp)
        End If

    End Sub


    Private Sub cboSortType_Changed() Handles cboSortType.SelectionChanged

        If Me.IsInitialized Then

            With cvsStepExperiments

                .SortDescriptions.Clear()

                Select Case cboSortType.SelectedIndex

                    Case 0  'by yield
                        .SortDescriptions.Add(New SortDescription("Yield", ListSortDirection.Descending))

                    Case 1 'by scale
                        .SortDescriptions.Add(New SortDescription("RefReactantGrams", ListSortDirection.Descending))

                End Select

                cvsStepExperiments.View.Refresh()

            End With

        End If

    End Sub



    ''' <summary>
    ''' Scales the summary compound ViewBox in a way to obtain an uniform bond length across all components.
    ''' </summary>
    ''' <param name="cpdView">ViewBox containing the components.</param>
    ''' 
    Private Sub ScaleViewBoxSize(ByRef cpdView As Viewbox, maxExtent As Double)

        With CType(cpdView.Child, Canvas)
            cpdView.Height = 0.21 * .Height
            cpdView.Width = 0.21 * .Width
        End With

        With cpdView

            '  Dim maxExtent = 400

            If .Height > maxExtent Then
                .Height = maxExtent
                .Width = .Width * (maxExtent / .Height)
            ElseIf .Width > maxExtent Then
                .Width = maxExtent
                .Height = .Height * (maxExtent / .Width)
            End If

            .Margin = New Thickness(-4)

        End With


    End Sub


End Class
