Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports ElnBase
Imports ElnCoreModel
Imports Microsoft.EntityFrameworkCore.Metadata.Internal

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

        'If seedSequence.SeedStep IsNot Nothing Then
        '    'seedSequence.SeedStep.IsSelected = True
        'End If

    End Sub


    ''' <summary>
    ''' Populates the sequence structure scheme panel with the structures contained in the specified sequence.
    ''' </summary>
    ''' 
    Private Sub PopulateSequenceScheme(sequence As SequenceControl)

        Dim seedStepStruct As SequenceStructure = Nothing

        pnlSeqStructures.Children.Clear()

        For Each stp In sequence.SequenceSteps

            Dim seqStruct As New SequenceStructure
            seqStruct.SourceStep = stp

            If stp Is sequence.SequenceSteps.First Then
                If sequence.HasUpstreamConnections Then
                    seqStruct.ShowLeftArrow()
                End If
            End If

            seqStruct.StructureCanvas = stp.GetReactantImage
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
            End If
        End With

        pnlSeqStructures.Children.Add(prodSeqStruct)

        'select seed step
        If seedStepStruct IsNot Nothing Then
            seedStepStruct.IsSelected = True
        End If

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

        PopulateSequenceScheme(CType(sender, SequenceControl))

    End Sub


    Private Sub SequenceStructure_StepArrowSelected(sender As Object)

        UnselectAllSteps()

        Dim struct = CType(sender, SequenceStructure)
        struct.IsSelected = True

    End Sub


    Private Sub pnlConnections_PreviewMouseDown(sender As Object, e As RoutedEventArgs) Handles pnlConnections.PreviewMouseDown

        If TypeOf e.Source Is SequenceControl Then
            RaiseEvent ClearSequenceSelections(Me)
        End If

    End Sub


End Class
