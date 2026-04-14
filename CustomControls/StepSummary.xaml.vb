Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Input
Imports ElnBase
Imports ElnCoreModel


Public Class StepSummary

    Public Shared Event RequestOpenExperiment(sender As Object, expEntry As tblExperiments, isFromServer As Boolean)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        cvsStepExperiments = Me.TryFindResource("cvsStepExperiments")

        AddHandler SketchArea.SketchSourceChanged, AddressOf SketchArea_SketchSourceChanged
        AddHandler ServerSync.ServerContextCreated, AddressOf ServerSync_ServerContextCreated
        AddHandler dlgServerConnection.ServerContextCreated, AddressOf ServerSync_ServerContextCreated

    End Sub

    Private Property ServerContext As ElnDbContext

    Private Property RefReactInChIKey As String

    Private Property RefProductInChIKey As String

    Private Property CurrUserID As String

    Private Property cvsStepExperiments As CollectionViewSource


    Private Sub Me_DataContextChanged() Handles Me.DataContextChanged

        'handles *tblUsers* context changes

        If Me.DataContext IsNot Nothing Then
            CurrUserID = CType(Me.DataContext, tblUsers).UserID
            UpdateStepExperiments()
            cboSortType_Changed()
        Else
            CurrUserID = ""
        End If

    End Sub


    Private Sub ServerSync_ServerContextCreated(dbContext As ElnDbContext)

        'handles server context changes from both the server sync control and the server connection dialog

        ServerContext = dbContext

        With chkIncludeServer
            .IsEnabled = (ServerContext IsNot Nothing)
            .IsChecked = My.Settings.SameStepAllUsers AndAlso .IsEnabled    'check only if enabled
        End With

        UpdateStepExperiments()

    End Sub


    ''' <summary>
    ''' Updates the list of same step experiments, combining local experiments with *finalized* server experiments of other users.
    ''' </summary>
    ''' 
    Private Sub UpdateStepExperiments()

        If Me.DataContext Is Nothing Then
            Exit Sub
        End If

        Dim localExperiments = CType(Me.DataContext, tblUsers).tblExperiments.Where(Function(exp) exp.ReactantInChIKey = RefReactInChIKey _
                                                                                      AndAlso exp.ProductInChIKey = RefProductInChIKey)
        If ServerContext IsNot Nothing AndAlso chkIncludeServer.IsChecked Then

            Dim serverExperiments = ServerContext.tblExperiments.
                Where(Function(exp) exp.ReactantInChIKey = RefReactInChIKey _
                        AndAlso exp.ProductInChIKey = RefProductInChIKey _
                        AndAlso exp.UserID <> CurrUserID _
                        AndAlso exp.WorkflowState = ELNEnumerations.WorkflowStatus.Finalized)

            cvsStepExperiments.Source = localExperiments.Concat(serverExperiments)

        Else

            cvsStepExperiments.Source = localExperiments

        End If

    End Sub


    Private Sub lstRssHits_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles lstStepExperiments.PreviewMouseUp


        Dim selItem = CType(lstStepExperiments.SelectedItem, tblExperiments)

        If selItem IsNot Nothing Then

            Dim fromServer = (selItem.UserID <> CurrUserID)
            RaiseEvent RequestOpenExperiment(Me, selItem, fromServer)

            e.Handled = True

        End If


    End Sub


    Private Sub chkIncludeServer_Changed() Handles chkIncludeServer.Checked, chkIncludeServer.Unchecked

        If Me.IsInitialized AndAlso chkIncludeServer.IsMouseOver Then   'IsMouseOver detects user interaction
            UpdateStepExperiments()
            My.Settings.SameStepAllUsers = chkIncludeServer.IsChecked
        End If

    End Sub


    Private Sub SketchArea_SketchSourceChanged(sender As Object, skInfo As SketchResults) 'shared event

        If skInfo IsNot Nothing AndAlso skInfo.Reactants.Count > 0 AndAlso skInfo.Products.Count > 0 Then

            RefReactInChIKey = skInfo.Reactants.First.InChIKey
            RefProductInChIKey = skInfo.Products.First.InChIKey

            UpdateStepExperiments()

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

            btnMatTotals.IsEnabled = True

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

            btnMatTotals.IsEnabled = False

        End If

    End Sub


    Private Sub lnkPubChemReact_Click() Handles lnkPubChemReact.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://pubchem.ncbi.nlm.nih.gov/compound/" + blkInChIKeyReact.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub lnkPubChemProd_Click() Handles lnkPubChemProd.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://pubchem.ncbi.nlm.nih.gov/compound/" + blkInChIKeyProd.Text)
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


    Private Sub btnMatTotals_PreviewMouseUp() Handles btnMatTotals.PreviewMouseUp

        Dim mainWdw = WPFToolbox.FindVisualParent(Of Window)(Me)
        Dim totalsDlg As New dlgExperimentInfo
        With totalsDlg
            .Owner = mainWdw
            .DataContext = ExperimentContent.TabExperimentsPresenter.DataContext
            .ShowDialog()
        End With

    End Sub


    Private Sub ListBoxItem_Selected(sender As Object, e As MouseButtonEventArgs) Handles lstStepExperiments.PreviewMouseUp

        Dim selExp = lstStepExperiments.SelectedItem

        If selExp IsNot Nothing Then
            lstStepExperiments.UnselectAll()
            RaiseEvent RequestOpenExperiment(Me, selExp, False)
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


End Class
