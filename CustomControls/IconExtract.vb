
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Interop
Imports System.Windows.Media.Imaging

''' <summary>
''' Gets the BitmapImage of the specified size from the icon of the specified file.
''' </summary>

Public Class IconExtract

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Structure SHFILEINFO
        Public hIcon As IntPtr
        Public iIcon As Integer
        Public dwAttributes As UInteger
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
        Public szDisplayName As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=80)>
        Public szTypeName As String
    End Structure

    <DllImport("shell32.dll", CharSet:=CharSet.Auto)>
    Private Shared Function SHGetFileInfo(pszPath As String,
        dwFileAttributes As UInteger,
        ByRef psfi As SHFILEINFO,
        cbFileInfo As UInteger,
        uFlags As UInteger) As IntPtr
    End Function

    <DllImportAttribute("User32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function DestroyIcon(ByVal hIcon As IntPtr) As Integer
    End Function

    Private Const SHGFI_ICON As UInteger = &H100
    Private Const SHGFI_SMALLICON As UInteger = &H1
    Private Const SHGFI_LARGEICON As UInteger = &H0

    ''' <summary>
    ''' Gets the BitmapImage of the specified size from the icon of the specified file.
    ''' </summary>
    ''' <param name="filePath">The path of the file.</param>
    ''' <param name="largeIcon">True for large icon, false for small icon.</param>
    ''' <returns>The BitmapImage of the file icon.</returns>

    Public Shared Function GetFileIconAsBitmapImage(filePath As String, largeIcon As Boolean) As BitmapImage

        Dim sizeOption = If(largeIcon, SHGFI_LARGEICON, SHGFI_SMALLICON)

        Dim shinfo As New SHFILEINFO()
        SHGetFileInfo(filePath, 0, shinfo, CUInt(Marshal.SizeOf(shinfo)), SHGFI_ICON Or sizeOption)

        ' Create Icon from handle
        Dim icon As Icon = Icon.FromHandle(shinfo.hIcon)

        ' Convert to BitmapSource (WPF)
        Dim source As BitmapSource = Imaging.CreateBitmapSourceFromHIcon(
        icon.Handle,
        Int32Rect.Empty,
        BitmapSizeOptions.FromEmptyOptions())

        ' Freeze for cross-thread use
        source.Freeze()

        ' Convert BitmapSource to BitmapImage
        Dim bmpImage As New BitmapImage()
        Dim encoder As New PngBitmapEncoder()
        encoder.Frames.Add(BitmapFrame.Create(source))

        Using ms As New IO.MemoryStream()
            encoder.Save(ms)
            ms.Position = 0
            bmpImage.BeginInit()
            bmpImage.CacheOption = BitmapCacheOption.OnLoad
            bmpImage.StreamSource = ms
            bmpImage.EndInit()
            bmpImage.Freeze()
        End Using

        Dim res = DestroyIcon(DestroyIcon(shinfo.hIcon))

        Return bmpImage

    End Function

End Class


