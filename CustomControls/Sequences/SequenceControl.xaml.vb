
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes
Imports ElnBase
Imports ElnCoreModel


Public Class SequenceControl

    Public Enum SequenceDirection
        Downstream
        Upstream
    End Enum


    ''' <summary>
    ''' Sets or gets the current running upstream sequence number
    ''' </summary>
    ''' 
    Public Shared Property UpstreamSequenceNr As Integer


    ''' <summary>
    ''' Sets or gets the current running downstream sequence number
    ''' </summary>
    ''' 
    Public Shared Property DownstreamSequenceNr As Integer


    ''' <summary>
    ''' Sets or gets all sequences present in the downstream panel of this sequence
    ''' </summary>
    ''' 
    Private DownstreamSequences As New List(Of SequenceControl)


    ''' <summary>
    ''' Sets or gets all sequences present in the upstream panel of this sequence
    ''' </summary>
    ''' 
    Private UpstreamSequences As New List(Of SequenceControl)


    ''' <summary>
    ''' Sets of gets the step from which the sequence build was initiated. Is nothing for all non-seed sequences.
    ''' </summary>
    ''' 
    Public Property SeedStep As SequenceStep


    ''' <summary>
    ''' Sets or gets the title of the sequence
    ''' </summary>
    ''' 
    Public Property SequenceTitle As String
        Get
            Return blkMainTitle.Text
        End Get
        Set(value As String)
            blkMainTitle.Text = value
        End Set
    End Property


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference reactant at the start of the multi-step synthetic reaction sequence. 
    ''' </summary>
    ''' 
    Public Property StartInChIKey As String


    ''' <summary>
    ''' Sets or gets the InChIKey of the reference product at the end of the multi-step synthetic reaction sequence. 
    ''' </summary>
    ''' 
    Public Property EndInChIKey As String


    ''' <summary>
    ''' Sets or gets a list of the synthetic steps the ReactionSequence consists of, ordered by their position in the sequence.
    ''' </summary>
    ''' 
    Public Property SequenceSteps As New List(Of SequenceStep)


    ''' <summary>
    ''' Sets or gets if the sequence has downstream children
    ''' </summary>
    ''' 
    Public Property HasDownstreamConnections As Boolean = False


    ''' <summary>
    ''' Sets or gets if the sequence has an upstream parent
    ''' </summary>
    ''' 
    Public Property HasUpstreamConnections As Boolean = False


    ''' <summary>
    ''' Sets or gets a list of all downstream sequence groups which converge to their right to a 
    ''' single sequence or material.
    ''' </summary>
    ''' 
    Friend Property ConvergingDownstreamGroups As New List(Of List(Of SequenceControl))


    ''' <summary>
    ''' Sets or gets a list of all upstream sequence groups which converge to their left to a 
    ''' single sequence or material.
    ''' </summary>
    ''' 
    Friend Property ConvergingUpstreamGroups As New List(Of List(Of SequenceControl))


    ''' <summary>
    ''' Occurs when this control is clicked
    ''' </summary>
    ''' 
    Public Shared Event SequenceSelected(sender As Object)


    ''' <summary>
    ''' For XAML designer only
    ''' </summary>
    ''' 
    Public Sub New()

        InitializeComponent()

    End Sub


    ''' <summary>
    ''' Creates a new reaction sequence based on the specified experiment entry, then recursively adds all downstream and 
    ''' upstream steps. Additional sequences are recursively added on the way.
    ''' </summary>
    ''' <param name="initialExp">The seed experiment to build the sequence around.</param>
    ''' <param name="dbContext">The database context (local or server based).</param>
    ''' 
    Public Sub New(initialExp As tblExperiments, dbContext As ElnDbContext)

        InitializeComponent()

        StartInChIKey = initialExp.ReactantInChIKey
        EndInChIKey = initialExp.ProductInChIKey

        DownstreamSequenceNr = 1
        UpstreamSequenceNr = 0

        Dim firstStep As New SequenceStep(initialExp.ReactantInChIKey, initialExp.ProductInChIKey, dbContext)
        firstStep.IsSeedStep = True
        SequenceSteps.Add(firstStep)
        SeedStep = firstStep

        AddDownstreamElements(firstStep)
        AddUpstreamElements(firstStep)

        SequenceTitle = "➤ Main Sequence"

    End Sub


    ''' <summary>
    ''' Creates a new reaction sequence based on the specified step and direction, then recursively adds all downstream and 
    ''' upstream steps and additional sequences.
    ''' </summary>
    ''' <param name="connectingStep">The first or last step (defined by the SequenceDirection parameter) of a sequence to connect to.</param>
    ''' <param name="direction">The direction of the connection (upstream or downstream).</param>
    ''' 
    Friend Sub New(connectingStep As SequenceStep, direction As SequenceDirection)

        InitializeComponent()

        StartInChIKey = connectingStep.ReactantInChIKey
        EndInChIKey = connectingStep.ProductInChIKey

        If Not SequenceSteps.Contains(connectingStep) Then
            SequenceSteps.Add(connectingStep)
        End If

        If direction = SequenceDirection.Downstream Then
            DownstreamSequenceNr += 1
            Me.SequenceTitle = "Sequence " + DownstreamSequenceNr.ToString
            AddDownstreamElements(connectingStep)
        Else
            UpstreamSequenceNr -= 1
            Me.SequenceTitle = "Sequence " + UpstreamSequenceNr.ToString
            AddUpstreamElements(connectingStep)
        End If

    End Sub


    Private Sub Me_Loaded() Handles Me.Loaded

        UpdateLayout()

        CreateVerticalConnectorLines()

        AddHandler dlgSequences.ClearSequenceSelections, AddressOf dlgSequences_ClearSequenceSelections

    End Sub


    Public Sub CreateVerticalConnectorLines()

        If HasDownstreamConnections Then

            VertPolylineRight.Points.Clear()

            '  draw left vertical connect lines connecting all sequences
            For Each seq In DownstreamSequences
                Dim attachPointLeft = seq.GetVerticalAttachmentPointLeft
                Dim attachPoint = seq.TranslatePoint(attachPointLeft, pnlDownstream)
                If attachPoint.Y > 0 Then
                    If seq IsNot DownstreamSequences.First Then
                        attachPoint.Y += 2
                    End If
                    VertPolylineRight.Points.Add(attachPoint)
                End If
            Next

            '  additionally draw vertical connect lines for connecting individual merging sequence groups to their right
            For Each mergeGroup In ConvergingDownstreamGroups
                Dim mergeLine As New Polyline With {.StrokeThickness = 2, .Stroke = New SolidColorBrush(ColorConverter.ConvertFromString("#FFC283FC"))}
                For Each seq In mergeGroup
                    Dim attachPointRight = seq.GetVerticalAttachmentPointRight
                    Dim attachPoint = seq.TranslatePoint(attachPointRight, canvOverlayRight)
                    If attachPoint.Y > 0 Then
                        If seq IsNot DownstreamSequences.First Then
                            attachPoint.Y += 2
                        End If
                        mergeLine.Points.Add(attachPoint)
                    End If
                Next
                canvOverlayRight.Children.Add(mergeLine)
            Next

        End If


        If HasUpstreamConnections Then

            VertPolylineLeft.Points.Clear()

            '  draw right vertical connect lines connecting all sequences
            For Each seq In UpstreamSequences
                Dim attachPointRight = seq.GetVerticalAttachmentPointRight
                Dim attachPoint = seq.TranslatePoint(attachPointRight, canvOverlayLeft)
                If attachPoint.Y > 0 Then
                    If seq Is UpstreamSequences.Last Then
                        attachPoint.Y += 2
                    End If
                    VertPolylineLeft.Points.Add(attachPoint)

                    Debug.WriteLine(attachPoint.X.ToString + "; " + attachPoint.Y.ToString)

                End If
            Next

            Debug.WriteLine("")

            '  additionally draw vertical connect lines for connecting individual merging sequence groups to their left
            For Each mergeGroup In ConvergingUpstreamGroups
                Dim mergeLine As New Polyline With {.StrokeThickness = 2, .Stroke = New SolidColorBrush(ColorConverter.ConvertFromString("#FFC283FC"))}
                For Each seq In mergeGroup
                    Dim attachPointRight = seq.GetVerticalAttachmentPointLeft
                    Dim attachPoint = seq.TranslatePoint(attachPointRight, canvOverlayLeft)
                    If attachPoint.Y > 0 Then
                        If seq IsNot UpstreamSequences.First Then
                            attachPoint.Y += 2
                        End If
                        mergeLine.Points.Add(attachPoint)
                    End If
                Next
                canvOverlayLeft.Children.Add(mergeLine)
            Next

        End If

    End Sub


    ''' <summary>
    ''' Gets the attachment point of the horizontal upstream connector line in control coordinates, 
    ''' or point value (0,0) if an error occurred.
    ''' </summary>
    '''
    Public Function GetVerticalAttachmentPointLeft() As Point

        Return horizConnLeft.TranslatePoint(New Point(0, 0), Me)

    End Function


    ''' <summary>
    ''' Gets the attachment point of the horizontal downstream connector line in control coordinates, 
    ''' or point value (0,0) if an error occurred.
    ''' </summary>
    '''
    Public Function GetVerticalAttachmentPointRight() As Point

        Return downConnector.TranslatePoint(New Point(downConnector.ActualWidth, 0), Me)

    End Function


    ''' <summary>
    ''' Recursively adds downstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddDownstreamElements(refStep As SequenceStep)

        Dim endReached As Boolean = False

        While Not endReached

            'Otherwise get next connects
            Dim nextConnects = refStep.GetNextSteps

            Select Case nextConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add next step, if not upstream converging one

                    refStep = nextConnects.First

                    'complete sequence if refStep has multiple incoming sequences and does not contain seed step
                    Dim prevConnects = refStep.GetPreviousSteps
                    If prevConnects.Count > 1 AndAlso Not SequenceSteps.Contains(SeedStep) Then
                        endReached = True
                        Exit While
                    End If

                    SequenceSteps.Add(refStep)
                    EndInChIKey = refStep.ProductInChIKey

                Case > 1 'multiple downstream connects -> branch off

                    HasDownstreamConnections = True

                    'create (recursive) downstream elements 
                    For Each connStep In nextConnects
                        Dim downSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive: constructor builds adjacent elements based on connStep
                        downSequence.ShowUpstreamConnector()
                        downSequence.HasUpstreamConnections = True
                        DownstreamSequences.Add(downSequence)   'add to list, not UI
                    Next

                    'detect converging downstream sequences
                    Dim res = From seq In DownstreamSequences Group By seq.EndInChIKey Into convergentGroups = Group

                    For Each result In res

                        Dim downStrElement As New ParallelSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downstream panel
                            Dim seq = result.convergentGroups.First
                            downStrElement.pnlConvSequences.Children.Add(seq)

                        Else

                            'convergent sequences -> add converging sequence group

                            ConvergingDownstreamGroups.Add(result.convergentGroups.ToList)

                            For Each seq In result.convergentGroups
                                seq.ShowDownstreamConnector()
                                seq.HasDownstreamConnections = True
                                downStrElement.pnlConvSequences.Children.Add(seq)
                            Next

                            Dim finalStep = result.convergentGroups.First.SequenceSteps.Last
                            If finalStep.GetNextSteps.Count > 0 Then

                                'merging to a downstream sequence

                                Dim connStep = finalStep.GetNextSteps.First
                                Dim convergedSequence As New SequenceControl(connStep, SequenceDirection.Downstream) 'recursive
                                convergedSequence.HasUpstreamConnections = True

                                With downStrElement
                                    .pnlConvergingDown.Visibility = Visibility.Visible
                                    .pnlConvergingDown.Children.Add(convergedSequence)
                                End With

                            Else

                                'merging to common product without further sequence

                                With downStrElement
                                    .btnMergeRightCenter.Visibility = Visibility.Visible
                                    .pnlConvergingDown.Visibility = Visibility.Visible
                                End With

                            End If
                        End If

                        pnlDownstream.Children.Add(downStrElement)

                    Next

                    endReached = True

                    ShowDownstreamConnector()

            End Select

        End While

    End Sub


    ''' <summary>
    ''' Converts 1 to 'a', 2 to 'b', etc.
    ''' </summary>
    ''' 
    Private Function NumberToCharacter(val As Integer) As String

        Return ChrW((AscW("a"c) + val))

    End Function


    ''' <summary>
    ''' Recursively adds upstream steps and sequences relative to the specified step
    ''' </summary>
    ''' 
    Private Sub AddUpstreamElements(refStep As SequenceStep)

        Dim endReached As Boolean = False

        While Not endReached

            'Otherwise get next connects
            Dim prevConnects = refStep.GetPreviousSteps

            Select Case prevConnects.Count

                Case 0 'no more connecting steps, end of sequence

                    endReached = True

                Case 1  'sequence continues -> add previous step

                    refStep = prevConnects.First

                    'complete sequence if refStep has multiple incoming sequences and does not contain seed step
                    Dim nextConnects = refStep.GetNextSteps
                    If nextConnects.Count > 1 AndAlso Not SequenceSteps.Contains(SeedStep) Then
                        endReached = True
                        Exit While
                    End If

                    SequenceSteps.Insert(0, refStep)
                    StartInChIKey = refStep.ReactantInChIKey

                Case > 1 'multiple upstream connects -> branch off

                    HasUpstreamConnections = True
                    ' VerticalConnectorLeft.Visibility = Visibility.Visible

                    'create (recursive) upstream elements 
                    For Each connStep In prevConnects
                        Dim upSequence As New SequenceControl(connStep, SequenceDirection.Upstream) 'recursive
                        upSequence.ShowDownstreamConnector()
                        upSequence.HasDownstreamConnections = True
                        UpstreamSequences.Add(upSequence)   'add to list, not UI
                    Next

                    'detect converging upstream sequences
                    Dim res = From seq In UpstreamSequences Group By seq.StartInChIKey Into convergentGroups = Group

                    For Each result In res

                        Dim upStrElement As New ParallelSequenceItem

                        If result.convergentGroups.Count = 1 Then

                            'no convergence -> add next connecting sequence to downstream panel
                            Dim seq = result.convergentGroups.First
                            upStrElement.pnlConvSequences.Children.Add(seq)

                        Else

                            'convergent sequences -> add converging sequence group

                            ConvergingUpstreamGroups.Add(result.convergentGroups.ToList)

                            For Each seq In result.convergentGroups
                                seq.ShowUpstreamConnector()
                                seq.HasUpstreamConnections = True
                                upStrElement.pnlConvSequences.Children.Add(seq)
                            Next

                            Dim firstStep = result.convergentGroups.First.SequenceSteps.First
                            If firstStep.GetPreviousSteps.Count > 0 Then

                                'merging to a upstream sequence

                                Dim connStep = firstStep.GetPreviousSteps.First
                                Dim convergedSequence As New SequenceControl(connStep, SequenceDirection.Upstream) 'recursive
                                With convergedSequence
                                    .ShowDownstreamConnector()
                                    .HasDownstreamConnections = True
                                End With

                                With upStrElement
                                    .pnlConvergingUp.Visibility = Visibility.Visible
                                    .pnlConvergingUp.Children.Add(convergedSequence)
                                End With

                            Else

                                'merging to common product without further sequence

                                With upStrElement
                                    .btnMergeLeftCenter.Visibility = Visibility.Visible
                                    .pnlConvergingUp.Visibility = Visibility.Visible
                                End With

                            End If
                        End If

                        pnlUpstream.Children.Add(upStrElement)

                    Next

                    endReached = True

                    ShowUpstreamConnector()

            End Select

        End While

    End Sub


    Private Sub ShowUpstreamConnector()

        upConnector.Visibility = Visibility.Visible

    End Sub


    Private Sub ShowDownstreamConnector()

        downConnector.Visibility = Visibility.Visible

    End Sub


    Private Sub btnMain_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnMain.Click

        btnMain.IsChecked = True
        RaiseEvent SequenceSelected(Me)

    End Sub


    ''' <summary>
    ''' Typically used for unchecking previous button when another one is checked
    ''' </summary>
    '''
    Public Sub dlgSequences_ClearSequenceSelections(sender As Object)

        btnMain.IsChecked = False

    End Sub


    ''' <summary>
    ''' Highlights and selects this SequenceControl
    ''' </summary>
    ''' 
    Public Sub HighlightControl()

        btnMain.IsChecked = True

    End Sub


End Class











