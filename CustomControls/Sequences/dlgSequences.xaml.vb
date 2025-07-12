Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Media
Imports ElnBase
Imports ElnCoreModel


Public Class dlgSequences


    Public Shared Event ClearSequenceSelections(sender As Object)


    Public Sub New(refExperiment As tblExperiments, localDbContext As ElnDbContext, serverDbContext As ElnDbContext, useServer As Boolean)

        ' This call is required by the designer.
        InitializeComponent()

        ReferenceExperiment = refExperiment
        LocalContext = localDbContext
        ServerContext = serverDbContext
        UseServerContext = useServer

        AddHandler SequenceControl.SequenceSelected, AddressOf SequenceControl_SequenceClicked
        AddHandler SequenceStructure.StepArrowSelected, AddressOf SequenceStructure_StepArrowSelected

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        cboItemServer.IsEnabled = (ServerContext IsNot Nothing)
        cboDataContext.SelectedItem = If(cboItemServer.IsEnabled AndAlso UseServerContext, cboItemServer, cboItemLocal)

    End Sub


    ''' <summary>
    ''' Sets or gets the current experiment, on which the initial sequence is built. 
    ''' </summary>
    ''' 
    Private Property ReferenceExperiment As tblExperiments


    ''' <summary>
    ''' Sets of gets the local experiments database context.
    ''' </summary>
    ''' 
    Private Property LocalContext As ElnDbContext


    ''' <summary>
    ''' Sets of gets the server experiments database context.
    ''' </summary>
    ''' 
    Public Shared Property ServerContext As ElnDbContext


    ''' <summary>
    ''' Sets of gets if the server database should be used instead of the local one.
    ''' </summary>
    ''' 
    Public Shared Property UseServerContext As Boolean = False


    ''' <summary>
    ''' Calculate and display the sequence containing the current experiment.
    ''' </summary>
    ''' 
    Private Sub Me_ContentRendered() Handles Me.ContentRendered

        'ToDo: Disable 'Synthesis' button if current experiment contains no sketch

        If ReferenceExperiment.RxnSketch Is Nothing Then
            Exit Sub
        End If

        Cursor = Cursors.Wait
        ForceCursor = True

        BuildSequences(ReferenceExperiment)

        Cursor = Cursors.Arrow
        ForceCursor = False

    End Sub


    Private Sub cboDataContext_SelectionChanged() Handles cboDataContext.SelectionChanged

        UseServerContext = (cboDataContext.SelectedItem Is cboItemServer)
        My.Settings.UseServerSequences = UseServerContext

        BuildSequences(ReferenceExperiment)

    End Sub


    ''' <summary>
    ''' Display and select current reference step.
    ''' </summary>
    ''' 
    Private Sub lnkRefStep_PreviewMouseUp(sender As Object, e As RoutedEventArgs)   'XAML risen event

        Me_ContentRendered()

    End Sub


    ''' <summary>
    ''' Builds the synthetic step sequence the specified experiment is part of, including all connected 
    ''' downstream and upstream sequences, then highlights the seed sequence and the step the reference 
    ''' experiment is part of.
    ''' </summary>
    ''' <param name="refExperiment">The experiment the sequence is based on.</param>
    ''' 
    Private Sub BuildSequences(refExperiment As tblExperiments)

        Dim dbContext As ElnDbContext = If(UseServerContext AndAlso ServerContext IsNot Nothing, ServerContext, LocalContext)

        Dim seedSequence = New SequenceControl(refExperiment, dbContext)
        With pnlConnections.Children
            .Clear()
            .Add(seedSequence)
        End With

        seedSequence.HighlightControl()
        PopulateSequenceScheme(seedSequence)

    End Sub


    ''' <summary>
    ''' Populates the sequence structure scheme panel with the structures contained in the specified sequence.
    ''' </summary>
    ''' 
    Private Sub PopulateSequenceScheme(sequence As SequenceControl)

        Dim seedStepStruct As SequenceStructure = Nothing
        Dim stepPos As Integer = 1

        pnlSeqStructures.Children.Clear()

        ' Set global reaction component foreground color (for reactant and product canvases in displayed sequence)
        SketchResults.ComponentStructureColor = Brushes.Yellow

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
                .blkSeedSymbol.Visibility = If(stp.IsSeedStep, Visibility.Visible, Visibility.Collapsed)

                Dim stepExpList = stp.GetStepExperiments
                Dim maxYield = (From exp In stepExpList Order By exp.Yield Descending).FirstOrDefault.Yield
                Dim yieldPrefix = If(stepExpList.Count > 1, "≤ ", "")
                .blkExpCount.Text = If(maxYield Is Nothing, "no yield", yieldPrefix + ELNCalculations.SignificantDigitsString(maxYield, 3) + "%")

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
