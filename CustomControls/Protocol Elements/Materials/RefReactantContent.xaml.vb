Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class RefReactantContent

    ''' <summary>
    ''' Finalized experiments: Displays relevant undisplayed material data in tooltip. 
    ''' Densities are only shown if a weight/volume conversion is applied.
    ''' </summary>
    ''' 
    Private Sub Me_MouseEnter() Handles Me.MouseEnter

        Dim refReactantEntry = CType(Me.DataContext, tblRefReactants)

        If refReactantEntry.ProtocolItem.Experiment.WorkflowState = WorkflowStatus.Finalized Then
            With refReactantEntry
                Dim mwString = "MW: " + Format(.MolecularWeight, "0.00")
                Dim densityStr = If(.Density IsNot Nothing AndAlso .IsDisplayAsVolume, "; Density: " + Format(.Density, "0.00"), "")
                ToolTip = .Name + " - " + mwString + densityStr
            End With
        Else
            ToolTip = Nothing
        End If

    End Sub

End Class
