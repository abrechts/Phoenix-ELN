Imports ElnBase.ELNEnumerations
Imports ElnCoreModel


Public Class AuxiliaryContent

    ''' <summary>
    ''' Finalized experiments: ToolTip displays the material density, if a weight/volume conversion was applied.
    ''' </summary>
    ''' 
    Private Sub Me_MouseEnter() Handles Me.MouseEnter

        Dim auxiliaryEntry = CType(Me.DataContext, tblAuxiliaries)

        If auxiliaryEntry.ProtocolItem.Experiment.WorkflowState = WorkflowStatus.Finalized Then
            With auxiliaryEntry
                If .Density IsNot Nothing Then
                    ToolTip = .Name + " - " + "Density: " + Format(.Density, "0.00")
                Else
                    ToolTip = .Name + " - " + "Density: --"
                End If
            End With
        Else
            ToolTip = Nothing
        End If

    End Sub

End Class
