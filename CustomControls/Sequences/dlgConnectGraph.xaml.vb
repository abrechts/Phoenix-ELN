
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Threading
Imports ElnBase
Imports ElnCoreModel


Partial Public Class dlgConnectGraph


    ''' <summary>
    ''' Sets or gets if additional debug info is displayed, e.g. a tooltip on the cards and a seed object selector.
    ''' </summary>
    ''' 
    Public Shared Property IsDebugMode As Boolean = False
    ' --------------------------------------------------

    Private _srcObjects As New List(Of SequenceGraph.GraphObject)

    Private Property LocalExperiments As IQueryable(Of tblExperiments)
    Private Property ServerExperiments As IQueryable(Of tblExperiments)
    Private Property QueryExperiments As IQueryable(Of tblExperiments)
    Private Property SeedExperiment As tblExperiments


    Public Sub New(refExp As tblExperiments, localContext As ElnDbContext, serverContext As ElnDbContext, useServer As Boolean)

        InitializeComponent()

        SeedExperiment = refExp
        lnkRefExp.Text = refExp.ExperimentID

        LocalExperiments = localContext.tblExperiments
        If serverContext IsNot Nothing Then
            ServerExperiments = serverContext.tblExperiments
        End If

        If useServer AndAlso serverContext IsNot Nothing Then
            QueryExperiments = ServerExperiments
        Else
            QueryExperiments = LocalExperiments
            useServer = False
        End If

        With cboSourceSelection
            cboItemServer.IsEnabled = (serverContext IsNot Nothing)
            .SelectedItem = If(.IsEnabled AndAlso useServer, cboItemServer, cboItemLocal)
        End With

        AddHandler GraphView.SequenceCardClicked, AddressOf OnSequenceCardClicked
        AddHandler SequenceStructure.StepArrowClicked, AddressOf OnStepArrowClicked
        AddHandler StepExpSelector.RequestOpenExperiment, AddressOf OnStepExpItemClicked

    End Sub


    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)

        If SeedExperiment.RxnSketch Is Nothing Then
            Exit Sub
        End If

        If IsDebugMode Then
            pnlSeedSelector.Visibility = Visibility.Visible
        End If

        CreateConnections()

    End Sub


    Private Sub Window_KeyDown(sender As Object, e As Input.KeyEventArgs)

        If e.Key = Input.Key.Escape Then Close()

    End Sub



    ''' <summary>
    ''' Parses through the connection graph (BFS traversal) and returns a single experiment per connection node.
    ''' </summary>
    ''' <param name="seedExp">The experiment at the origin of the connection graph.</param>
    ''' <param name="allExperiments">The collection of all experiments to consider.</param>
    '''
    Private Sub SetGraphItems(seedExp As tblExperiments, allExperiments As IQueryable(Of tblExperiments))

        'NOTE: connKey-based deduplication doesn't distinguish between same step experiments of different users!

        QueryExperiments = allExperiments

        'lightweight projection for BFS: avoids pulling large fields (sketches, MDL files, fingerprints, ...) for the full table
        Dim allExpKeys = allExperiments.Select(Function(e) New ConnNode With {
            .Id = e.ExperimentID,
            .ReactantInChIKey = e.ReactantInChIKey,
            .IsRacemicReactant = e.IsRacemicReactant,
            .ProductInChIKey = e.ProductInChIKey,
            .IsRacemicProduct = e.IsRacemicProduct
        }).ToList()

        Dim visitedConnKeys As New HashSet(Of String)
        Dim queue As New Queue(Of ConnNode)
        Dim orderedIds As New List(Of String)

        Dim seedNode = allExpKeys.First(Function(n) n.Id = seedExp.ExperimentID)

        visitedConnKeys.Add(ConnKey(seedNode))

        queue.Enqueue(seedNode)
        orderedIds.Add(seedNode.Id)

        While queue.Count > 0

            Dim current = queue.Dequeue()

            Dim neighbors = allExpKeys.Where(Function(n) (n.ReactantInChIKey = current.ProductInChIKey AndAlso n.IsRacemicReactant = current.IsRacemicProduct) OrElse
                (n.ProductInChIKey = current.ReactantInChIKey AndAlso n.IsRacemicProduct = current.IsRacemicReactant))

            For Each node In neighbors

                If visitedConnKeys.Add(ConnKey(node)) Then
                    orderedIds.Add(node.Id)
                    queue.Enqueue(node)
                End If

            Next

        End While

        'Server-side-only query: Fetch full entities (incl. large fields) only for the items that will actually be displayed
        Dim fullExperiments = allExperiments.Where(Function(e) orderedIds.Contains(e.ExperimentID)).ToDictionary(Function(e) e.ExperimentID)

        Dim connItems As New List(Of SequenceGraph.GraphObject)
        For i = 0 To orderedIds.Count - 1
            connItems.Add(GetGraphObject(fullExperiments(orderedIds(i)), isSeed:=(i = 0)))
        Next

        _srcObjects = connItems

    End Sub


    Private Sub cboSourceSelection_SelectionChanged() Handles cboSourceSelection.SelectionChanged

        ExpEntryEnabledConverter.IsServerExperiments = (cboSourceSelection.SelectedIndex = 1)

        If cboSourceSelection.SelectedItem IsNot Nothing AndAlso cboSourceSelection.IsMouseOver Then

            'mouseover ensures direct user interaction, not from code
            Dim useServer As Boolean = (cboSourceSelection.SelectedItem Is cboItemServer)
            If useServer AndAlso ServerExperiments IsNot Nothing Then
                QueryExperiments = ServerExperiments
            Else
                QueryExperiments = LocalExperiments
            End If

            My.Settings.UseServerSequences = (QueryExperiments Is ServerExperiments)

            CreateConnections()

        End If

    End Sub


    Private Sub OnSequenceCardClicked(seq As SequenceGraph.SequenceNode)

        PopulateSequenceScheme(seq)

    End Sub


    Private Sub OnStepArrowClicked(struct As SequenceStructure)

        'deselect all, then select target
        For Each stp As SequenceStructure In pnlSeqStructures.Children
            stp.IsSelected = False
        Next
        struct.IsSelected = True

        'update selected step experiments
        Dim stepExp = struct.StepExperiments
        stepExpItems.ItemsSource = stepExp.ToList

        runSchemePath.Text = blkSchemeTitle.Text
        runStepNumber.Text = struct.StepNumberStr

    End Sub


    Private Sub CreateConnections()

        SetGraphItems(SeedExperiment, QueryExperiments)

        If _srcObjects.Count = 0 Then
            LoadSampleData()
        End If

        BindSidebar()

        Dim seed = _srcObjects.First

        Try
            GraphView.BuildAndRender(_srcObjects, seed)
            GraphView.SelectSequence(GraphView.SeedSequence)
            OnSequenceCardClicked(GraphView.SeedSequence)  'render reaction schemes
            AdjustSplitterToGraphHeight()
            ApplyFitAllZoomAfterLayout()

        Catch ex As Exception

        End Try

    End Sub


    ''' <summary>
    ''' Defers SequenceGraph.ApplyFitAllZoom until after the pending layout pass, so it sees the
    ''' ScrollViewer viewport size that results from the GraphAreaRow height set by AdjustSplitterToGraphHeight.
    ''' </summary>
    '''
    Private Sub ApplyFitAllZoomAfterLayout()

        Dispatcher.BeginInvoke(Sub() GraphView.ApplyFitAllZoom(), DispatcherPriority.Loaded)

    End Sub


    ''' <summary>
    ''' Resizes the graph area row (above pnlSplitter) to fit the height of the
    ''' just-rendered sequence graph, so the splitter sits directly below it.
    ''' </summary>
    '''
    Private Sub AdjustSplitterToGraphHeight()

        Const GRAPH_AREA_CHROME As Double = 70    'header row + GraphView margins above/below the canvas
        Const MIN_GRAPH_AREA_HEIGHT As Double = 150

        UpdateGraphAreaMaxHeight()

        Dim desiredHeight = Math.Max(GraphView.GraphContentHeight + GRAPH_AREA_CHROME, MIN_GRAPH_AREA_HEIGHT)

        Dim maxHeight = GraphAreaRow.MaxHeight
        If maxHeight > MIN_GRAPH_AREA_HEIGHT Then
            desiredHeight = Math.Min(desiredHeight, maxHeight)
        End If

        GraphAreaRow.Height = New GridLength(desiredHeight)

    End Sub


    ''' <summary>
    ''' Caps the height GraphAreaRow can grow to, leaving at least BOTTOM_AREA_MIN_HEIGHT for the
    ''' reaction scheme area below the splitter. GridSplitter does not reliably enforce the
    ''' MinHeight of the Star-sized row below it, so the cap is enforced on this (Pixel-sized) row instead.
    ''' </summary>
    '''
    Private Sub Window_SizeChanged(sender As Object, e As SizeChangedEventArgs) Handles Me.SizeChanged

        UpdateGraphAreaMaxHeight()

    End Sub


    Private Sub UpdateGraphAreaMaxHeight()

        Const BOTTOM_AREA_MIN_HEIGHT As Double = 250    'must match Row1's MinHeight in the XAML

        GraphAreaRow.MaxHeight = Math.Max(GraphAreaRow.MinHeight, Me.ActualHeight - BOTTOM_AREA_MIN_HEIGHT)

    End Sub


    ''' <summary>
    ''' Populates the individual step structures of the sequence scheme based on the provided sequence.
    ''' </summary>
    ''' 
    Private Sub PopulateSequenceScheme(seq As SequenceGraph.SequenceNode)

        'global reaction component foreground color (for reactant and product canvases in displayed sequence)
        SketchResults.ComponentStructureColor = New SolidColorBrush(ColorConverter.ConvertFromString("#FFF9F96B"))  'light yellow

        blkSchemeTitle.Text = $"Sequence {seq.Id + 1}"

        'color legend
        Dim accent = GraphView.GetSequenceAccentBrush(seq)
        bdrSchemeTitle.BorderBrush = accent
        If TypeOf accent Is SolidColorBrush Then
            Dim c = CType(accent, SolidColorBrush).Color
            bdrSchemeTitle.Background = New SolidColorBrush(Color.FromArgb(60, c.R, c.G, c.B))
        End If

        bdrLegendSwatch.Background = accent
        blkLegendType.Text = seq.SequenceType

        Dim stepNr As Integer = 1
        pnlSeqStructures.Children.Clear()

        For Each seqStep In seq.Members

            Dim expItem = CType(seqStep.ConnectItem.AttachedItem, tblExperiments)
            Dim skInfo = DrawingEditor.GetSketchInfo(expItem.RxnSketch)
            Dim reactCanv = skInfo.Reactants.First.StructureCanvas

            Dim stepStruct As New SequenceStructure
            With stepStruct

                'add reactant structure for each step
                .SetStepExperiments(expItem, QueryExperiments)
                .StructureCanvas = reactCanv
                .StepNumberStr = "Step " + stepNr.ToString

                If seqStep.ConnectItem.IsSeed Then
                    'always initially seed step, if present in sequence
                    .blkSeedSymbol.Visibility = Visibility.Visible
                    OnStepArrowClicked(stepStruct)
                ElseIf stepNr = 1 Then
                    'otherwise select first step of sequence
                    OnStepArrowClicked(stepStruct)
                End If

                Dim yieldStr = Format(.StepExperiments.Max(Function(x) x.Yield), "0")
                Dim prefix = If(.StepExperiments.Count > 1, "≤ ", "")
                If yieldStr <> "" Then
                    .blkExpCount.Text = prefix + yieldStr + "%"
                Else
                    .blkExpCount.Text = "no yield"
                End If

                stepNr += 1

                If seqStep Is seq.Members.First AndAlso seq.Incoming.Count > 0 Then
                    .ShowLeftArrow()
                End If

                pnlSeqStructures.Children.Add(stepStruct)

                'additionally add product structure for final step

                If seqStep Is seq.Members.Last Then
                    Dim stepStructProd As New SequenceStructure
                    With stepStructProd
                        Dim prodCanv = skInfo.Products.First.StructureCanvas
                        .StructureCanvas = prodCanv
                        .IsSelected = False
                        .HideRightArrow()
                        pnlSeqStructures.Children.Add(stepStructProd)
                        If seq.Outgoing.Count > 0 Then
                            .ShowDottedRightArrow()
                        End If
                    End With
                End If

            End With

        Next

    End Sub


    ''' <summary>
    ''' Lightweight projection of the connectivity-relevant fields of a tblExperiments row.
    ''' </summary>
    ''' 
    Private Structure ConnNode

        Public Id As String
        Public ReactantInChIKey As String
        Public IsRacemicReactant As Byte?
        Public ProductInChIKey As String
        Public IsRacemicProduct As Byte?

    End Structure


    ''' <summary>
    ''' Returns the compound node identity (InChIKey + racemic flag) used as graph node key.
    ''' </summary>
    '''
    Private Shared Function NodeKey(inChIKey As String, isRacemic As Byte?) As String

        Return inChIKey & "-" & isRacemic.ToString

    End Function


    ''' <summary>
    ''' Returns the connection (reactant node | product node) identity used to dedupe experiments.
    ''' </summary>
    '''
    Private Shared Function ConnKey(node As ConnNode) As String

        Return NodeKey(node.ReactantInChIKey, node.IsRacemicReactant) & " | " & NodeKey(node.ProductInChIKey, node.IsRacemicProduct)

    End Function


    Private Function GetGraphObject(exp As tblExperiments, Optional isSeed As Boolean = False) As SequenceGraph.GraphObject

        Dim obj As New SequenceGraph.GraphObject With {
            .A = NodeKey(exp.ReactantInChIKey, exp.IsRacemicReactant),
            .B = NodeKey(exp.ProductInChIKey, exp.IsRacemicProduct),
            .IsSeed = isSeed,
            .Title = $"{exp.ReactantInChIKey.Substring(0, 4)}-{exp.IsRacemicReactant} → {exp.ProductInChIKey.Substring(0, 4)}-{exp.IsRacemicProduct}",
            .AttachedItem = exp
        }
        Return obj

    End Function


    Private Sub LoadSampleData()

        _srcObjects = New List(Of SequenceGraph.GraphObject) From {
            New SequenceGraph.GraphObject With {.A = "QARV-0", .B = "VWDD-0"},
            New SequenceGraph.GraphObject With {.A = "VWDD-0", .B = "HUMN-0"},
            New SequenceGraph.GraphObject With {.A = "HUMN-0", .B = "WAPN-0"},
            New SequenceGraph.GraphObject With {.A = "VWDD-0", .B = "HUMN-1"},
            New SequenceGraph.GraphObject With {.A = "HUMN-1", .B = "WAPN-0"},
            New SequenceGraph.GraphObject With {.A = "WAPN-0", .B = "YEZN-0"},
            New SequenceGraph.GraphObject With {.A = "WAPN-0", .B = "FCRS-0"},
            New SequenceGraph.GraphObject With {.A = "WAPN-0", .B = "OVGI-0"},
            New SequenceGraph.GraphObject With {.A = "YEZN-0", .B = "BVUR-0"},
            New SequenceGraph.GraphObject With {.A = "FCRS-0", .B = "BVUR-0"},
            New SequenceGraph.GraphObject With {.A = "BVUR-0", .B = "YUNJ-0"},
            New SequenceGraph.GraphObject With {.A = "YUNJ-0", .B = "BSBY-0"}
        }

    End Sub

    Private Sub BindSidebar()

        SeedComboBox.ItemsSource = Nothing
        SeedComboBox.ItemsSource = _srcObjects
        ' Use the preferred seed name set by LoadSampleData; fall back to index 0
        ' for randomized data where no preferred name is set.

        SeedComboBox.SelectedIndex = 0

    End Sub


    Private Sub SeedComboBox_SelectionChanged() Handles SeedComboBox.SelectionChanged

        If SeedComboBox.SelectedItem Is Nothing Then
            Return
        End If

        Dim seed = CType(SeedComboBox.SelectedItem, SequenceGraph.GraphObject)

        'update seed status of all objects
        For Each obj In _srcObjects
            obj.IsSeed = False
        Next
        seed.IsSeed = True

        Try
            GraphView.BuildAndRender(_srcObjects, seed)
            GraphView.SelectSequence(GraphView.SeedSequence)
            OnSequenceCardClicked(GraphView.SeedSequence)  'render reaction schemes
            AdjustSplitterToGraphHeight()
            ApplyFitAllZoomAfterLayout()
        Catch ex As Exception

        End Try

    End Sub


    Private Sub OnStepExpItemClicked(sender As Object, exp As tblExperiments, isFromServer As Boolean, args As StepExpOpenArgs)

        If args.WasOpened Then
            infoToast.Show($"{exp.ExperimentID} opened in protocol area")
        End If

    End Sub


    Private Sub Window_Closed(sender As Object, e As EventArgs) Handles Me.Closed

        RemoveHandler StepExpSelector.RequestOpenExperiment, AddressOf OnStepExpItemClicked

    End Sub


    Private Sub icoInfo_PreviewMouseUp() Handles icoInfo.PreviewMouseUp

        Dim info As New ProcessStartInfo("https://abrechts.github.io/phoenix-eln-help.github.io/pages/SyntheticConnections.html")
        info.UseShellExecute = True
        Process.Start(info)

    End Sub


End Class
