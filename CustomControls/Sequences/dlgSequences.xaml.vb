
Imports System.Windows
Imports ElnBase
Imports ElnCoreModel

Public Class dlgSequences


    Public Shared Event ClearSequenceSelections(sender As Object)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        AddHandler SequenceControl.SequenceSelected, AddressOf SequenceControl_SequenceClicked
        AddHandler SequenceStructure.StepArrowSelected, AddressOf SequenceStructure_StepArrowSelected

    End Sub


    ''' <summary>
    ''' Builds the synthetic step sequence the specified experiment is part of, including all connected 
    ''' downstream and upstream sequences, then highlights the seed sequence and the step the reference 
    ''' experiment is part of.
    ''' </summary>
    ''' <param name="refExperiment"></param>
    ''' 
    Public Sub BuildSequences(refExperiment As tblExperiments, dbContext As ElnDbContext)

        Dim seedSequence = New SequenceControl(refExperiment, dbContext)
        pnlConnections.Children.Add(seedSequence)

        seedSequence.HighlightControl()
        PopulateSequenceScheme(seedSequence)

        blkSeedExpId.Text = " ➔ " + refExperiment.ExperimentID + " ➔ "

    End Sub


    ''' <summary>
    ''' Populates the sequence structure scheme panel with the structures contained in the specified sequence.
    ''' </summary>
    ''' 
    Private Sub PopulateSequenceScheme(sequence As SequenceControl)

        Dim seedStepStruct As SequenceStructure = Nothing
        Dim stepPos As Integer = 1

        blkSequenceTitle.Text = sequence.SequenceTitle

        pnlSeqStructures.Children.Clear()

        For Each stp In sequence.SequenceSteps

            Dim seqStruct As New SequenceStructure
            seqStruct.SourceStep = stp
            stp.AssignedSequenceStructure = seqStruct

            If stp Is sequence.SequenceSteps.First Then
                If sequence.HasUpstreamConnections Then
                    seqStruct.ShowLeftArrow()
                End If
            End If

            With seqStruct
                .StructureCanvas = stp.GetReactantImage
                .blkStepNr.Text = "Step " + stepPos.ToString
                .blkExpCount.Text = stp.GetStepExperiments.Count.ToString + " exp"
            End With

            stepPos += 1
            pnlSeqStructures.Children.Add(seqStruct)

            If stp Is sequence.SeedStep Then
                seedStepStruct = seqStruct
            End If

        Next

        'add final product structure
        Dim prodSeqStruct As New SequenceStructure
        With prodSeqStruct
            .StructureCanvas = sequence.SequenceSteps.Last.GetProductImage
            If Not sequence.HasDownstreamConnections Then
                .HideRightArrow()   'hide arrow if no more downstream sequences
            Else
                .ShowDottedRightArrow()
            End If
        End With

        pnlSeqStructures.Children.Add(prodSeqStruct)

        CurrentSequence = sequence

        'select seed step
        If seedStepStruct IsNot Nothing Then
            SelectStep(seedStepStruct.SourceStep)
        End If

    End Sub


    ''' <summary>
    ''' Selects the specified sequence step.
    ''' </summary>
    ''' 
    Private Sub SelectStep(targetStep As SequenceStep)

        UnselectAllSteps()

        With targetStep.AssignedSequenceStructure
            stepSelector.DataContext = .SourceStep
            blkSeqTitle.Text = CurrentSequence.SequenceTitle
            lblStepName.Text = .blkStepNr.Text
            .IsSelected = True
        End With

    End Sub


    ''' <summary>
    ''' Unselects all step selections in the reaction sequence scheme.
    ''' </summary>
    ''' 
    Public Sub UnselectAllSteps()

        For Each struct In pnlSeqStructures.Children
            If TypeOf struct Is SequenceStructure Then
                CType(struct, SequenceStructure).IsSelected = False
            End If
        Next

    End Sub


    Private Sub SequenceControl_SequenceClicked(sender As Object)

        Dim seq = CType(sender, SequenceControl)
        PopulateSequenceScheme(seq)

        SelectStep(seq.SequenceSteps.First)

    End Sub


    Public Property CurrentSequence As SequenceControl


    Private Sub SequenceStructure_StepArrowSelected(sender As Object)

        Dim struct = CType(sender, SequenceStructure)
        SelectStep(struct.SourceStep)

    End Sub


    Private Sub pnlConnections_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles pnlConnections.PreviewMouseDown

        If TypeOf e.Source Is SequenceControl Then
            RaiseEvent ClearSequenceSelections(Me)
        End If

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/SyntheticConnections.html")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


End Class
