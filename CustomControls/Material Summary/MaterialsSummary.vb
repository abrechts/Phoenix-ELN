Imports ElnBase
Imports ElnCoreModel

Public Class MaterialsSummary

    ''' <summary>
    ''' Gets the reagents of the specified experiment entry grouped by name and source as List(Of MaterialTotal).
    ''' </summary>
    ''' 
    Public Shared Function GetRefReactantGroups(expEntry As tblExperiments) As List(Of MaterialTotal)

        Dim groupRes = From protEntry In expEntry.tblProtocolItems Where protEntry.tblRefReactants IsNot Nothing
                       Group By matName = protEntry.tblRefReactants.Name, matSource = protEntry.tblRefReactants.Source Into Group,
                              totalGrams = Sum(protEntry.tblRefReactants.Grams)
                       Order By matName Ascending

        Dim materialGroups As New List(Of MaterialTotal)

        For Each matGroup In groupRes

            Dim matTotal As New MaterialTotal
            Dim scaledTotal = ELNCalculations.ScaleWeight(matGroup.totalGrams)
            With matTotal
                .Amount = scaledTotal.Amount
                .Unit = scaledTotal.Unit
                .MaterialName = matGroup.matName
                .Source = matGroup.matSource
            End With

            If matGroup.Group.Count > 1 Then
                'only create sub-node if more then one entry
                Dim pos As Integer = 0
                For Each protItem In matGroup.Group
                    Dim matEntry As New MaterialEntry
                    Dim scaledVol = ELNCalculations.ScaleWeight(protItem.tblRefReactants.Grams)
                    pos += 1
                    With matEntry
                        .Amount = scaledVol.Amount
                        .Unit = scaledVol.Unit
                        .MaterialName = protItem.tblRefReactants.Name
                        .Source = protItem.tblRefReactants.Source
                        .Position = pos
                    End With
                    matTotal.MaterialEntries.Add(matEntry)
                Next
            End If

            materialGroups.Add(matTotal)

        Next

        Return materialGroups

    End Function


    ''' <summary>
    ''' Gets the reagents of the specified experiment entry grouped by name and source as List(Of MaterialTotal).
    ''' </summary>
    ''' 
    Public Shared Function GetReagentGroups(expEntry As tblExperiments) As List(Of MaterialTotal)

        Dim groupRes = From protEntry In expEntry.tblProtocolItems Where protEntry.tblReagents IsNot Nothing
                       Group By matName = protEntry.tblReagents.Name, matSource = protEntry.tblReagents.Source Into Group,
                              totalGrams = Sum(protEntry.tblReagents.Grams)
                       Order By matName Ascending

        Dim materialGroups As New List(Of MaterialTotal)

        For Each matGroup In groupRes

            Dim matTotal As New MaterialTotal
            Dim scaledTotal = ELNCalculations.ScaleWeight(matGroup.totalGrams)
            With matTotal
                .Amount = scaledTotal.Amount
                .Unit = scaledTotal.Unit
                .MaterialName = matGroup.matName
                .Source = matGroup.matSource
            End With

            If matGroup.Group.Count > 1 Then
                'only create sub-node if more then one entry
                Dim pos As Integer = 0
                For Each protItem In matGroup.Group
                    Dim matEntry As New MaterialEntry
                    Dim scaledVol = ELNCalculations.ScaleWeight(protItem.tblReagents.Grams)
                    pos += 1
                    With matEntry
                        .Amount = scaledVol.Amount
                        .Unit = scaledVol.Unit
                        .MaterialName = protItem.tblReagents.Name
                        .Source = protItem.tblReagents.Source
                        .Position = pos
                    End With
                    matTotal.MaterialEntries.Add(matEntry)
                Next
            End If

            materialGroups.Add(matTotal)

        Next

        Return materialGroups

    End Function


    ''' <summary>
    ''' Gets the solvents of the specified experiment entry grouped by name and source as List(Of MaterialTotal).
    ''' </summary>
    ''' 
    Public Shared Function GetSolventGroups(expEntry As tblExperiments) As List(Of MaterialTotal)

        Dim solventsRes = From protEntry In expEntry.tblProtocolItems Where protEntry.tblSolvents IsNot Nothing
                          Group By matName = protEntry.tblSolvents.Name, matSource = protEntry.tblSolvents.Source Into Group,
                              totalMl = Sum(protEntry.tblSolvents.Milliliters)
                          Order By matName Ascending

        Dim materialGroups As New List(Of MaterialTotal)

        For Each matGroup In solventsRes

            Dim solventTotal As New MaterialTotal
            Dim scaledTotal = ELNCalculations.ScaleVolume(matGroup.totalMl)
            With solventTotal
                .Amount = scaledTotal.Amount
                .Unit = scaledTotal.Unit
                .MaterialName = matGroup.matName
                .Source = matGroup.matSource
            End With

            If matGroup.Group.Count > 1 Then
                'only create sub-node if more then one entry
                Dim pos As Integer = 0
                For Each protItem In matGroup.Group
                    Dim matEntry As New MaterialEntry
                    Dim scaledVol = ELNCalculations.ScaleVolume(protItem.tblSolvents.Milliliters)
                    pos += 1
                    With matEntry
                        .Amount = scaledVol.Amount
                        .Unit = scaledVol.Unit
                        .MaterialName = protItem.tblSolvents.Name
                        .Source = protItem.tblSolvents.Source
                        .Position = pos
                    End With
                    solventTotal.MaterialEntries.Add(matEntry)
                Next
            End If

            materialGroups.Add(solventTotal)

        Next

        Return materialGroups

    End Function


    ''' <summary>
    ''' Gets the auxiliaries of the specified experiment entry grouped by name and source as List(Of MaterialTotal).
    ''' </summary>
    ''' 
    Public Shared Function GetAuxiliariesGroups(expEntry As tblExperiments) As List(Of MaterialTotal)

        Dim groupRes = From protEntry In expEntry.tblProtocolItems Where protEntry.tblAuxiliaries IsNot Nothing
                       Group By matName = protEntry.tblAuxiliaries.Name, matSource = protEntry.tblAuxiliaries.Source Into Group,
                              totalGrams = Sum(protEntry.tblAuxiliaries.Grams)
                       Order By matName Ascending

        Dim materialGroups As New List(Of MaterialTotal)

        For Each matGroup In groupRes

            Dim matTotal As New MaterialTotal
            Dim scaledTotal = ELNCalculations.ScaleWeight(matGroup.totalGrams)
            With matTotal
                .Amount = scaledTotal.Amount
                .Unit = scaledTotal.Unit
                .MaterialName = matGroup.matName
                .Source = matGroup.matSource
            End With

            If matGroup.Group.Count > 1 Then
                'only create sub-node if more then one entry
                Dim pos As Integer = 0
                For Each protItem In matGroup.Group
                    Dim matEntry As New MaterialEntry
                    Dim scaledVol = ELNCalculations.ScaleWeight(protItem.tblAuxiliaries.Grams)
                    pos += 1
                    With matEntry
                        .Amount = scaledVol.Amount
                        .Unit = scaledVol.Unit
                        .MaterialName = protItem.tblAuxiliaries.Name
                        .Source = protItem.tblAuxiliaries.Source
                        .Position = pos
                    End With
                    matTotal.MaterialEntries.Add(matEntry)
                Next
            End If

            materialGroups.Add(matTotal)

        Next

        Return materialGroups

    End Function

End Class


Public Class MaterialTotal

    Public Property Amount As Double
    Public Property Unit As String
    Public Property MaterialName As String
    Public Property Source As String
    Public Property MaterialEntries As New List(Of MaterialEntry)

End Class

Public Class MaterialEntry

    Public Property Amount As Double
    Public Property Unit As String
    Public Property MaterialName As String
    Public Property Source As String
    Public Property Position As Integer

End Class

