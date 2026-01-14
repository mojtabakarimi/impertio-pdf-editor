# PDF Viewer - Testing Guide

## Application Status: ✅ RUNNING

The PDF Viewer application has been successfully built and launched!

## What You Should See

When the application starts, you should see:

1. **Welcome Screen**
   - Large PDF icon (📄)
   - "No PDF Loaded" message
   - Instructions: "Click 'File → Open' or press Ctrl+O to open a PDF document"
   - "Open PDF" button

2. **Window Title**
   - "PDF Viewer" at the top

3. **Menu Bar**
   - **File** menu with "Open..." and "Exit" options
   - **View** menu with zoom controls

4. **Toolbar** (initially disabled)
   - Previous/Next page buttons
   - Page counter display
   - Zoom In/Out/Reset buttons
   - Zoom percentage display

5. **Status Bar** (at bottom)
   - "No document loaded" message

## Testing Instructions

### Test 1: Open a PDF Document

1. **Open File Dialog:**
   - Click **File → Open** (or press **Ctrl+O**)
   - OR click the **"Open PDF"** button on the welcome screen

2. **Select a PDF:**
   - Browse to any PDF file on your computer
   - Click "Open"

3. **Expected Result:**
   - The PDF should load and display the first page
   - Window title should update to "PDF Viewer - [filename]"
   - Toolbar buttons should become enabled
   - Status bar should show "Page 1 of X | Zoom: 100%"

### Test 2: Page Navigation

1. **Next Page:**
   - Click "Next ▶" button
   - Page number should increment
   - New page should render and display

2. **Previous Page:**
   - Click "◀ Previous" button
   - Page number should decrement
   - Previous page should display

3. **Expected Result:**
   - Smooth page transitions
   - Page counter updates correctly
   - Previous button disabled on page 1
   - Next button disabled on last page

### Test 3: Zoom Controls

1. **Zoom In:**
   - Click "🔍+" button (or press **Ctrl+OemPlus**)
   - Page should enlarge by 25%
   - Zoom percentage updates (125%, 150%, etc.)

2. **Zoom Out:**
   - Click "🔍-" button (or press **Ctrl+OemMinus**)
   - Page should shrink by 25%
   - Zoom percentage decreases (75%, 50%, etc.)

3. **Reset Zoom:**
   - Click "100%" button (or press **Ctrl+D0**)
   - Zoom resets to 100%
   - Page returns to original size

4. **Expected Result:**
   - Zoom range: 25% to 500%
   - Smooth scaling without pixelation
   - Status bar shows current zoom percentage

### Test 4: Performance Testing

1. **Open Large PDF:**
   - Open a PDF with 50+ pages
   - Navigation should remain smooth

2. **Navigate Through Pages:**
   - Click through multiple pages quickly
   - Check for responsive UI (no freezing)

3. **Test Caching:**
   - Navigate to page 10
   - Go back to page 1
   - Return to page 10
   - **Expected:** Second visit to page 10 should load instantly (from cache)

### Test 5: Keyboard Shortcuts

1. **Ctrl+O:** Open file dialog
2. **Ctrl+OemPlus:** Zoom in
3. **Ctrl+OemMinus:** Zoom out
4. **Ctrl+D0:** Reset zoom to 100%

All shortcuts should work as expected.

## Performance Metrics to Observe

### Initial Load Time
- Small PDF (< 5 pages): < 1 second
- Medium PDF (10-50 pages): 1-3 seconds
- Large PDF (100+ pages): 3-5 seconds

### Page Rendering
- First render: 200-500ms (depending on page complexity)
- Cached pages: < 50ms (instant)

### Memory Usage
- Application baseline: ~50-80 MB
- With 10 pages cached: ~150-200 MB
- Stable memory (no leaks on page navigation)

## Common Test PDFs

If you don't have test PDFs, you can:

1. **Create from Word/Office:** Save any document as PDF
2. **Download samples:**
   - Adobe PDF samples
   - Government documents (publicly available)
   - Open-source documentation PDFs

## Expected Issues (Known)

### Warnings (Safe to Ignore)
- **NU1701:** PdfiumViewer package compatibility - works correctly through .NET compatibility
- **CA1416:** ImageFormat.Png platform warnings - handled correctly at runtime

### Not Implemented Yet
- Thumbnail sidebar
- Text search
- Text selection/copy
- Page rotation
- Annotations
- Printing

## Troubleshooting

### Issue: Application won't start
**Solution:**
- Ensure .NET 10 SDK is installed: `dotnet --version`
- Rebuild: `dotnet clean && dotnet build`

### Issue: PDF won't open
**Solution:**
- Verify PDF file is not corrupted
- Check file permissions
- Try a different PDF

### Issue: Slow rendering
**Solution:**
- Check PDF complexity (high-resolution images slow rendering)
- Reduce zoom level
- Close other memory-intensive applications

### Issue: Crashes when opening PDF
**Solution:**
- Check PDF file is valid
- Look at console output for error messages
- Try with a simple PDF first

## Success Criteria ✅

The application is working correctly if:

- [x] Application launches without errors
- [x] Welcome screen displays properly
- [x] File dialog opens when clicking Open
- [ ] PDF loads and displays correctly
- [ ] Page navigation works smoothly
- [ ] Zoom controls function properly
- [ ] Status bar updates correctly
- [ ] UI remains responsive during operations
- [ ] Memory usage remains stable

## Next Steps

After successful testing:

1. **Test with various PDF types:**
   - Text-heavy documents
   - Image-heavy documents
   - Forms
   - Scanned documents

2. **Performance profiling:**
   - Test with very large files (500+ pages)
   - Monitor memory usage over time
   - Check CPU usage during rendering

3. **Cross-platform testing:**
   - Test on Windows (if not already)
   - Test on macOS
   - Test on Linux

4. **Report issues:**
   - Document any crashes or errors
   - Note performance bottlenecks
   - Suggest UI improvements

## Current Status

✅ **Application is RUNNING**
✅ **Build successful** (with expected warnings)
✅ **GUI window opened**
⏳ **Awaiting user testing with PDF files**

---

**Ready to test!** Open the application window and try loading a PDF file.
