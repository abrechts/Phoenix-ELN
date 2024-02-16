Imports ElnBase.ELNEnumerations
Imports ElnCoreModel

Public Class SolventContent

    ''' <summary>
    ''' Finalized experiments: ToolTip displays the material density, if a weight/volume conversion was applied.
    ''' </summary>
    ''' 
    Private Sub Me_MouseEnter() Handles Me.MouseEnter

        Dim solventEntry = CType(Me.DataContext, tblSolvents)

        If solventEntry.ProtocolItem.Experiment.WorkflowState = WorkflowStatus.Finalized Then
            With solventEntry
                If .Density IsNot Nothing AndAlso .IsDisplayAsWeight Then
                    ToolTip = .Name + " - " + "Density: " + Format(.Density, "0.00")
                Else
                    ToolTip = Nothing
                End If
            End With
        Else
            ToolTip = Nothing
        End If

    End Sub

End Class
