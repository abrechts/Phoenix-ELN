Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class ReagentContent

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Finalized experiments: Displays relevant undisplayed material data in tooltip. 
    ''' Densities are only shown if a weight/volume conversion is applied.
    ''' </summary>
    ''' 
    Private Sub Me_MouseEnter() Handles Me.MouseEnter

        Dim reagentEntry = CType(Me.DataContext, tblReagents)

        If reagentEntry.ProtocolItem.Experiment.WorkflowState = WorkflowStatus.Finalized Then
            With reagentEntry
                Dim mwStr = "MW: " + Format(.MolecularWeight, "0.00")
                Dim densityStr = If(.Density IsNot Nothing AndAlso .IsDisplayAsVolume,
                    "; Density: " + Format(.Density, "0.00"), "")
                ToolTip = .Name + " - " + mwStr + densityStr
            End With
        Else
            ToolTip = Nothing
        End If

    End Sub

End Class
