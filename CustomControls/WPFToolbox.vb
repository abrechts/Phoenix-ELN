Imports System.Collections.ObjectModel
Imports System.IO
Imports System.IO.Packaging
Imports System.Net
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Data
Imports System.Windows.Documents
Imports System.Windows.Input
Imports System.Windows.Markup
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Windows.Xps
Imports System.Windows.Xps.Packaging
Imports Microsoft.EntityFrameworkCore.Metadata.Internal
Imports Microsoft.Win32
Imports Key = System.Windows.Input.Key



Public Class WPFToolbox

    ''' <summary>
    ''' Waits until all WPF processes with the priority higher than the specified one have been completed
    ''' </summary>
    ''' <param name="priority">DispatcherPriority of all tasks to complete before continuing.</param>
    ''' <remarks>Code by Beatriz Stolnitz, Microsoft: http://bea.stollnitz.com/blog/?p=59</remarks>
    ''' 
    Public Shared Sub WaitForPriority(priority As DispatcherPriority)
        Try
            Dim frame As New DispatcherFrame()
            Dim dispatcherOperation As DispatcherOperation = Dispatcher.CurrentDispatcher.BeginInvoke(priority, New DispatcherOperationCallback(AddressOf ExitFrameOperation), frame)
            Dispatcher.PushFrame(frame)
            If dispatcherOperation.Status <> DispatcherOperationStatus.Completed Then
                dispatcherOperation.Abort()
            End If
        Catch ex As Exception
            'do nothing when called in too rapid succession
        End Try
    End Sub

    Private Shared Function ExitFrameOperation(obj As Object) As Object
        DirectCast(obj, DispatcherFrame).[Continue] = False
        Return Nothing
    End Function


    ''' <summary>
    ''' Scrolls the specified ListBoxItem completely into view.
    ''' </summary>
    ''' <param name="targetItem">The FrameworkElement to scroll into view.</param>
    ''' <param name="offset">The offset in pixels to the upper or lower ScrollViewer edge after scrolling.</param>
    ''' <remarks>The alternative BringIntoView does not always bring the element completely into view.</remarks>
    ''' 
    Public Shared Sub ScrollItemIntoView(parentScroller As ScrollViewer, itemsCtrl As ListBox, targetItem As FrameworkElement)

        itemsCtrl.ScrollIntoView(targetItem)

        Dim currPt As Point = targetItem.TranslatePoint(New Point(0, 0), parentScroller)

        '   WPFToolbox.WaitForPriority(DispatcherPriority.ContextIdle)

        Dim scrollPos = parentScroller.VerticalOffset

        If (currPt.Y + scrollPos + 6) > parentScroller.ViewportHeight Then
            parentScroller.ScrollToBottom()
        End If


    End Sub


    ''' <summary>
    ''' Converts a string to a textblock displaying numbers as subscripts
    ''' </summary>
    ''' <param name="srcText">String to convert.</param>
    ''' <param name="myFontFamily">Desired font family or returned textblock.</param>
    ''' <param name="myFontSize">Desired font size of returned textblock</param>
    ''' <returns>Textblock control with numeric subscripts.</returns>
    ''' <remarks>Useful e.g. for elemental formula notations, etc.</remarks>
    ''' 
    Public Shared Function Convert2NumericSubscriptBlock(ByVal srcText As String, ByVal myFontFamily As Media.FontFamily,
    ByVal myFontSize As Double) As TextBlock

        'produce EF text with subscripts
        Dim efBlk As New TextBlock
        Dim subSpan As Span
        Dim EfArr() As Char

        EfArr = srcText.ToCharArray()
        With efBlk
            .FontSize = myFontSize
            .FontFamily = myFontFamily
        End With
        For Each myChar As Char In EfArr
            If Val(myChar) > 0 OrElse myChar = "0" Then                 'DON'T use IsNumeric, it's dead slow!!
                subSpan = New Span
                With subSpan
                    .BaselineAlignment = BaselineAlignment.Subscript
                    .FontFamily = myFontFamily
                    .FontSize = 0.8 * myFontSize
                    .Inlines.Add(myChar)
                End With
                efBlk.Inlines.Add(subSpan)
            Else
                efBlk.Inlines.Add(myChar)
            End If
        Next

        efBlk.TextTrimming = TextTrimming.CharacterEllipsis
        efBlk.Arrange(New Rect)

        Return efBlk

    End Function


    ''' <summary>
    ''' Copies the contents of srcEntity to the contents of dstEntity
    ''' </summary>
    ''' <param name="srcEntity">The source entity to copy data from.</param>
    ''' <param name="dstEntity">The destination entity (the clone) to receive the data.</param>
    ''' 
    Public Shared Sub CloneEntityData(srcEntity As Object, dstEntity As Object)

        For Each prop In dstEntity.GetType.GetProperties
            Dim tmpProperty = srcEntity.GetType.GetProperty(prop.Name)
            If tmpProperty IsNot Nothing Then
                Dim tmpVal = srcEntity.GetType.GetProperty(prop.Name).GetValue(srcEntity)
                prop.SetValue(dstEntity, tmpVal)
            End If
        Next

    End Sub


    ' This method accepts two file paths to compare. A return value of 0 indicates that the contents of the files
    ' are the same. A return value of any other value indicates that the 
    ' files are not the same.

    Public Shared Function FileCompare(ByVal filePath1 As String, ByVal filePath2 As String) As Boolean
        Dim file1byte As Integer
        Dim file2byte As Integer
        Dim fs1 As FileStream
        Dim fs2 As FileStream

        ' Determine if the same file was referenced two times.
        If (filePath1 = filePath2) Then
            ' Return 0 to indicate that the files are the same.
            Return True
        End If

        ' Open the two files.
        fs1 = New FileStream(filePath1, FileMode.Open)
        fs2 = New FileStream(filePath2, FileMode.Open)

        ' Check the file sizes. If they are not the same, the files
        ' are not equal.
        If (fs1.Length <> fs2.Length) Then
            ' Close the file
            fs1.Close()
            fs2.Close()

            ' Return a non-zero value to indicate that the files are different.
            Return False
        End If

        ' Read and compare a byte from each file until either a
        ' non-matching set of bytes is found or until the end of
        ' file1 is reached.
        Do
            ' Read one byte from each file.
            file1byte = fs1.ReadByte()
            file2byte = fs2.ReadByte()
        Loop While ((file1byte = file2byte) And (file1byte <> -1))

        ' Close the files.
        fs1.Close()
        fs2.Close()

        ' Return the success of the comparison. "file1byte" is
        ' equal to "file2byte" at this point only if the files are 
        ' the same.
        Return ((file1byte - file2byte) = 0)
    End Function



    ''' <summary>
    ''' Determines if internet access is present.
    ''' </summary>
    ''' <returns>True, if available.</returns>
    ''' <remarks></remarks>
    ''' 
    Public Shared Function IsInternetAvailable() As Boolean

        Try
            Dim clnt As New Sockets.TcpClient("www.microsoft.com", 80)
            clnt.Close()
            Return True
        Catch
            Return False
        End Try

    End Function


    ' IP-Address Utility

    ''' <summary>
    ''' Gets a semicolon delimited string of local IP-addresses (ip4 and ip6)
    ''' </summary>
    ''' <returns>String containing local IP-Addresses of current machine.</returns>
    ''' <remarks></remarks>
    ''' 
    Public Shared Function GetMyInternalIPAddresses() As String

        Dim strHostName As String
        Dim IPAddresses As String = ""
        Dim count As Integer = 0

        ' First get the host name of local machine.
        strHostName = Net.Dns.GetHostName()

        ' Then using host name, get the IP address list..
        Dim ipEntry As IPHostEntry = Dns.GetHostEntry(strHostName)
        Dim addr As IPAddress() = ipEntry.AddressList

        For i = 0 To addr.Length - 1
            'explude ip6 and ip4 private & local addresses
            If Not addr(i).ToString.StartsWith("fe80::") Then
                count += 1
                If count = 1 Then
                    IPAddresses += addr(i).ToString
                Else
                    IPAddresses += "; " + addr(i).ToString
                End If
            End If
        Next

        Return IPAddresses

    End Function


    ''' <summary>
    ''' Merges several XPS documents into one
    ''' </summary>
    ''' <param name="sourcePaths"></param>
    ''' <param name="destPath"></param>
    ''' <remarks></remarks>
    ''' 
    Public Shared Sub MergeXPSDocs(ByVal sourcePaths As String(), ByVal destPath As String)

        Using container As Package = Package.Open(destPath, FileMode.Create)

            Dim xpsDoc As New XpsDocument(container, CompressionOption.Maximum)
            Dim fd As New FixedDocument()

            For Each sourceDocument As String In sourcePaths
                Using oldDoc As New XpsDocument(sourceDocument, FileAccess.Read)

                    Dim fds As FixedDocumentSequence = oldDoc.GetFixedDocumentSequence()
                    Dim dp As DocumentPaginator = fds.DocumentPaginator

                    For i As Integer = 0 To dp.PageCount - 1

                        Dim pc As New PageContent()
                        Dim fP As New FixedPage()
                        Dim oldPage As DocumentPage = dp.GetPage(i)

                        fP.Width = oldPage.Size.Width
                        fP.Height = oldPage.Size.Height
                        fP.Background = New VisualBrush(oldPage.Visual)

                        'prevents first page to be empty
                        Dim size = fd.DocumentPaginator.PageSize
                        fP.Measure(size)
                        fP.Arrange(New Rect(New Point(), size))
                        fP.UpdateLayout()

                        TryCast(pc, IAddChild).AddChild(fP)
                        fd.Pages.Add(pc)

                    Next
                End Using
            Next

            Dim xpsWriter As XpsDocumentWriter = XpsDocument.CreateXpsDocumentWriter(xpsDoc)

            xpsWriter.Write(fd)
            xpsDoc.Close()
            container.Close()

        End Using

    End Sub


    '------------------
    ' Image processing 
    '-------------------

    Public Shared Function ByteArrToBitmapImage(srcArr As Byte()) As BitmapImage

        Using ms As New IO.MemoryStream(srcArr)

            Dim bm As New BitmapImage

            bm.BeginInit()
            bm.CacheOption = BitmapCacheOption.OnLoad
            bm.StreamSource = ms
            bm.EndInit()

            Return bm

        End Using

    End Function


    ''' <summary>
    ''' Gets the BitmapImage of the specified image file, corresponding to the original image reduced in 
    ''' size to maxPixels. Returns nothing if the file is not a valid image file. 
    ''' </summary>
    ''' <param name="filePath">Path to the image file. Does not check if the file actually is an image file.</param>
    ''' <param name="maxPixels">Optional: The  pixel count to reduce the image size to (width or height, 
    ''' whatever is larger). A larger value than present in the original preserves the original 
    ''' image dimensions.</param>
    ''' 
    Public Shared Function BitmapImageFromFile(filePath As String, Optional maxPixels As Integer = Integer.MaxValue) As BitmapImage

        Dim imgBytes As Byte()

        ' get serialized image file
        Using fs As New FileStream(filePath, IO.FileMode.Open, IO.FileAccess.Read)
            Using binReader As New BinaryReader(fs)
                imgBytes = binReader.ReadBytes(fs.Length)
            End Using
        End Using

        ' create temp bitmap for determining image width
        Dim origBm As New BitmapImage
        Try
            Using testMs As New IO.MemoryStream(imgBytes)
                With origBm
                    .BeginInit()
                    .CacheOption = BitmapCacheOption.OnLoad
                    .StreamSource = testMs
                    .EndInit()
                End With
            End Using
        Catch ex As Exception
            Return Nothing 'invalid image file
        End Try

        If maxPixels = Integer.MaxValue Then
            'original image size
            Return origBm
        Else
            'reduce image size
            Dim reducedBm As New BitmapImage
            Using ms As New IO.MemoryStream(imgBytes)
                With reducedBm
                    .BeginInit()
                    .CacheOption = BitmapCacheOption.OnLoad
                    If origBm.Width > origBm.Height Then
                        If origBm.Width > maxPixels Then
                            .DecodePixelWidth = maxPixels
                        End If
                    Else
                        If origBm.Height > maxPixels Then
                            .DecodePixelHeight = maxPixels
                        End If
                    End If
                    .StreamSource = ms
                    .EndInit()
                End With
            End Using
            Return reducedBm
        End If

    End Function


    ''' <summary>
    ''' Encodes the specified BitmapImage to the specified imgFileExtension format (e.g. '.jpg' or '.png') 
    ''' and returns the bytes of the serialized image format. Nothing is returned for unknown formats.
    ''' </summary>
    ''' 
    Public Shared Function BitmapImageToBytes(bmImage As BitmapImage, imgFileExtension As String) As Byte()

        're-encode size reduced bitmap
        Dim newBytes = WPFToolbox.EncodeBitmapImage(bmImage, imgFileExtension)
        Return newBytes

    End Function


    ''' <summary>
    ''' Encodes a bitmap image source into the desired image format
    ''' </summary>
    ''' <param name="image">The image source to convert.</param>
    ''' <param name="imageFormat">The format, including ".". Supported: ".jpeg",".jpg",".bmp",".png", 
    ''' ".tiff", ".tif", ".gif", ".wmp"</param>
    ''' <returns>Image in the desired format as byte().</returns>
    ''' <remarks></remarks>
    ''' 
    Public Shared Function EncodeBitmapImage(ByVal image As ImageSource, ByVal imageFormat As String) As Byte()

        Dim result As Byte() = Nothing
        Dim encoder As BitmapEncoder

        Select Case (imageFormat.ToLower())
            Case ".jpg", ".jpeg"
                encoder = New JpegBitmapEncoder()
            Case ".bmp"
                encoder = New BmpBitmapEncoder()
            Case ".png"
                encoder = New PngBitmapEncoder()
            Case ".tiff", ".tif"
                encoder = New TiffBitmapEncoder()
            Case ".gif"
                encoder = New GifBitmapEncoder()
            Case ".wmp"
                encoder = New WmpBitmapEncoder()
            Case Else
                Return Nothing
        End Select

        If (TypeOf image Is BitmapSource) Then
            Dim stream As New MemoryStream
            encoder.Frames.Add(BitmapFrame.Create(image))
            encoder.Save(stream)
            stream.Seek(0, SeekOrigin.Begin)
            ReDim result(stream.Length)
            Dim br As New BinaryReader(stream)
            br.Read(result, 0, stream.Length)
            br.Close()
            stream.Close()
        End If
        Return result

    End Function


    ''' <summary>
    ''' Gets if the specified file currently is locked for the specified file access.
    ''' </summary>
    ''' 
    Public Shared Function IsFileLocked(ByVal filename As String, ByVal file_access As FileAccess) As Boolean

        Try
            Dim fs As New FileStream(filename, FileMode.Open, file_access)
            fs.Close()
            Return False
        Catch ex As Exception
            Return True
        End Try

    End Function




    ''' <summary>
    ''' Determines if the formal syntax of the specified e-mail address string is correct
    ''' </summary>
    ''' <param name="inputEmail">The e-mail address string to test.</param>
    ''' <returns>True, if syntax is correct</returns>
    ''' 
    Public Shared Function IsCorrectEMailAddress(inputEmail As String) As Boolean

        Dim strRegex As String = "^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}" & "\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\" & ".)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$"
        Dim re As New Regex(strRegex)
        If re.IsMatch(inputEmail) Then
            Return (True)
        Else
            Return (False)
        End If

    End Function



    ''' <summary>
    ''' Creates an arrow as a geometry group.
    ''' </summary>
    ''' <param name="fromPt">Starting point of arrow</param>
    ''' <param name="toPt">End point of arrow</param>
    ''' <param name="headWidth">Width of arrow head.</param>
    ''' <param name="headHeight">Height of arrow head.</param>
    ''' <returns>Arrow figure as geometry group.</returns>
    ''' <remarks>DrawGeometry with the return value as argument will provide line a fill brushes.</remarks>
    ''' 
    Public Shared Function CreateArrow(fromPt As Point, toPt As Point, headWidth As Double, headHeight As Double) As GeometryGroup

        Dim arrow As New GeometryGroup
        Dim arrowPath As New PathGeometry
        Dim arrowLine As New LineGeometry
        Dim arrowHead As New PathFigure
        Dim myline As New LineGeometry(fromPt, toPt)

        Dim x1 = fromPt.X
        Dim x2 = toPt.X
        Dim y1 = fromPt.Y
        Dim y2 = toPt.Y

        Dim theta As Double = Math.Atan2(y1 - y2, x1 - x2)
        Dim sint As Double = Math.Sin(theta)
        Dim cost As Double = Math.Cos(theta)

        Dim pt3 = New Point(
            x2 + (headWidth * cost - headHeight * sint),
            y2 + (headWidth * sint + headHeight * cost))

        Dim pt4 = New Point(
            x2 + (headWidth * cost + headHeight * sint),
            y2 - (headHeight * cost - headWidth * sint))

        With arrowHead
            .StartPoint = toPt
            With .Segments
                .Add(New LineSegment(pt3, False))   'upper edge
                .Add(New LineSegment(pt4, False))   'lower edge
                .Add(New LineSegment(toPt, False))
                .Add(New LineSegment(fromPt, False))
            End With
        End With
        arrowPath.Figures.Add(arrowHead)

        arrow.Children.Add(arrowPath)
        arrow.Children.Add(New LineGeometry(fromPt, toPt))

        Return arrow

    End Function

    ''' <summary>
    ''' Gets the current *localized* long date format, but without the full day name
    ''' </summary>
    ''' <returns>Shortened localized long date format without day name.</returns>
    ''' 
    Public Shared Function DayNameStrippedLongDateFormat()

        Dim longDatePattern = Globalization.DateTimeFormatInfo.CurrentInfo.LongDatePattern
        Dim res = Text.RegularExpressions.Regex.Replace(longDatePattern, "dddd,?", String.Empty).Trim()
        res = res.Replace("dd", "d")    'prevent e.g. May 03, 2012 (instead: May 3, 2012)
        res = res.Replace("MMM", "MM")  'use three-character month name, not full name

        Return res

    End Function



    Public Shared Function GetCell(dataGrid As DataGrid, row As DataGridRow, column As Integer) As DataGridCell

        If row IsNot Nothing Then
            Dim presenter As DataGridCellsPresenter = FindVisualChild(Of DataGridCellsPresenter)(row)
            If presenter Is Nothing Then
                ' if the row has been virtualized away, call its ApplyTemplate() method 
                '    * to build its visual tree in order for the DataGridCellsPresenter
                '    * and the DataGridCells to be created 
                row.ApplyTemplate()
                presenter = FindVisualChild(Of DataGridCellsPresenter)(row)
            End If
            If presenter IsNot Nothing Then
                Dim cell As DataGridCell = TryCast(presenter.ItemContainerGenerator.ContainerFromIndex(column), DataGridCell)
                If cell Is Nothing Then
                    ' bring the column into view
                    '                 * in case it has been virtualized away 

                    dataGrid.ScrollIntoView(row, dataGrid.Columns(column))
                    cell = TryCast(presenter.ItemContainerGenerator.ContainerFromIndex(column), DataGridCell)
                End If
                Return cell
            End If
        End If
        Return Nothing
    End Function


    Public Shared Function FindVisualChild(Of T As DependencyObject)(obj As DependencyObject) As T

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(obj) - 1

            Dim child As DependencyObject = VisualTreeHelper.GetChild(obj, i)

            If child IsNot Nothing AndAlso TypeOf child Is T Then
                Return DirectCast(child, T)
            Else
                Dim childOfChild As T = FindVisualChild(Of T)(child)
                If childOfChild IsNot Nothing Then
                    Return childOfChild
                End If
            End If

        Next

        Return Nothing

    End Function



    Public Shared Function FindVisualParent(Of T As DependencyObject)(child As DependencyObject) As T
        ' get parent item

        If TypeOf child IsNot Visual Then
            Return Nothing  'could be Run (e.g. for blue Now... link)
        End If

        Dim parentObject As DependencyObject = VisualTreeHelper.GetParent(child)

        ' we’ve reached the end of the tree
        If parentObject Is Nothing Then
            Return Nothing
        End If

        ' check if the parent matches the type we’re looking for
        Dim parent As T = TryCast(parentObject, T)
        If parent IsNot Nothing Then
            Return parent
        Else
            ' use recursion to proceed with next level
            Return FindVisualParent(Of T)(parentObject)
        End If
    End Function



    ''' <summary>
    ''' Converts the specified key to a localized character
    ''' </summary>
    '''
    Public Enum MapType As UInteger
        MAPVK_VK_TO_VSC = &H0
        MAPVK_VSC_TO_VK = &H1
        MAPVK_VK_TO_CHAR = &H2
        MAPVK_VSC_TO_VK_EX = &H3
    End Enum

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function ToUnicode(wVirtKey As UInteger, wScanCode As UInteger, lpKeyState As Byte(),
        <Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex:=4)> pwszBuff As Text.StringBuilder,
        cchBuff As Integer, wFlags As UInteger) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetKeyboardState(lpKeyState As Byte()) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function MapVirtualKey(uCode As UInteger, uMapType As MapType) As UInteger
    End Function

    ''' <summary>
    ''' Converts the specified key to a localized character.
    ''' </summary>

    Public Shared Function GetCharFromKey(key As Key) As System.Char
        Dim ch As Char = " "c

        Dim virtualKey As Integer = KeyInterop.VirtualKeyFromKey(key)
        Dim keyboardState As Byte() = New Byte(255) {}
        GetKeyboardState(keyboardState)

        Dim scanCode As UInteger = MapVirtualKey(CUInt(virtualKey), MapType.MAPVK_VK_TO_VSC)
        Dim stringBuilder As New Text.StringBuilder(2)

        Dim result As Integer = ToUnicode(CUInt(virtualKey), scanCode, keyboardState, stringBuilder, stringBuilder.Capacity, 0)
        Select Case result
            Case -1
                Exit Select
            Case 0
                Exit Select
            Case 1
                If True Then
                    ch = stringBuilder(0)
                    Exit Select
                End If
            Case Else
                If True Then
                    ch = stringBuilder(0)
                    Exit Select
                End If
        End Select

        Return ch

    End Function


    ''' <summary>
    ''' Gets the path to the current default browser.
    ''' </summary>
    ''' <returns>Full path to the default browser.</returns>
    ''' 
    Public Shared Function GetDefaultBrowser() As String

        Dim browser As String = String.Empty
        Dim key As RegistryKey = Nothing
        Try
            key = Registry.ClassesRoot.OpenSubKey("HTTP\shell\open\command", False)

            'trim off quotes
            browser = key.GetValue(Nothing).ToString().ToLower().Replace("""", "")
            If Not browser.EndsWith("exe") Then
                'get rid of everything after the ".exe"
                browser = browser.Substring(0, browser.LastIndexOf(".exe") + 4)
            End If
        Finally
            key?.Close()
        End Try
        Return browser

    End Function


    ''' <summary>
    ''' Gets the file path of a copied image file. Returns an empty string if no image file is present on the clipboard.
    ''' </summary>
    ''' 
    Public Shared Function GetClipboardImagePath() As String

        Dim dobj = Clipboard.GetDataObject()

        If dobj.GetDataPresent(DataFormats.FileDrop) Then
            Dim resArr As String() = dobj.GetData(DataFormats.FileDrop)
            If resArr.Length > 0 Then
                Return If(IsImageFile(resArr(0)), resArr(0), "")
            End If
        End If

        Return ""

    End Function


    ''' <summary>
    ''' Gets if the specified file path contains a supported image file extension.
    ''' </summary>
    ''' <param name="filePath">The file path to test.</param>
    '''
    Public Shared Function IsImageFile(filePath As String) As Boolean

        Dim ext = LCase(IO.Path.GetExtension(filePath))
        Dim imgList As New List(Of String)

        Dim collection = New String() {".jpg", ".jpeg", ".png", ".gif", ".giff", ".tif", ".tiff", ".emf", ".wmf"}
        imgList.AddRange(collection)

        Return imgList.Contains(ext)

    End Function


    Public Shared Sub ClearAllObjectBindings(dependencyObject As DependencyObject)

        For Each element As DependencyObject In EnumerateVisualDescendents(dependencyObject)
            BindingOperations.ClearAllBindings(element)
        Next

    End Sub


    Private Shared Function EnumerateVisualChildren(dependencyObject As DependencyObject) As IEnumerable(Of DependencyObject)

        Dim res As New List(Of DependencyObject)

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(dependencyObject) - 1
            res.Add(VisualTreeHelper.GetChild(dependencyObject, i))
        Next

        Return res

    End Function


    Private Shared Function EnumerateVisualDescendents(dependencyObject As DependencyObject) As IEnumerable(Of DependencyObject)

        Dim res As New List(Of DependencyObject)

        res.Add(dependencyObject)

        For Each child As DependencyObject In EnumerateVisualChildren(dependencyObject)
            For Each descendent As DependencyObject In EnumerateVisualDescendents(child)
                res.Add(descendent)
            Next
        Next

        Return res

    End Function


    ''' <summary>
    ''' Splits the provided string into a collection of strings with a maximum length per item.
    ''' </summary>
    ''' <param name="srcStr">The source string to split</param>
    ''' <param name="maxLength">The maximum length per item.</param>
    ''' <returns>The resulting collection of strings</returns>
    ''' 
    Public Shared Function SplitOnLength(srcStr As String, maxLength As Integer) As Collection(Of String)

        Dim index As Integer = 0
        Dim lines As New Collection(Of String)

        While index < srcStr.Length
            If index + maxLength < srcStr.Length Then
                lines.Add(srcStr.Substring(index, maxLength))
            Else
                lines.Add(srcStr.Substring(index))
            End If
            index += maxLength
        End While

        Return lines

    End Function


    ''' <summary>
    ''' Shortens the specified file name to the specified maximum length including and retaining its extension.
    ''' </summary>
    ''' <param name="fileName">The file name to shorten.</param>
    ''' <param name="maxLength">The maximum length of the file name.</param>
    ''' <returns>The shortened file name.</returns>
    '''
    Public Shared Function ShortenFileNameWithExtension(fileName As String, maxLength As Integer) As String

        Dim extension As String = Path.GetExtension(fileName)
        Dim nameWithoutExtension As String = Path.GetFileNameWithoutExtension(fileName)

        If nameWithoutExtension.Length + extension.Length > maxLength Then
            nameWithoutExtension = nameWithoutExtension.Substring(0, maxLength - extension.Length)
        End If

        Return nameWithoutExtension & extension

    End Function

End Class


