Imports ElnCoreModel

Public Class dlgExperimentInfo

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        tvReagentsSummary.ItemsSource = MaterialsSummary.GetReagentGroups(Me.DataContext)
        tvSolventsSummary.ItemsSource = MaterialsSummary.GetSolventGroups(Me.DataContext)

    End Sub



End Class
