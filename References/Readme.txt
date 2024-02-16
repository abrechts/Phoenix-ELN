
The components of this 'References' folder need to be placed into the root of the application directory 
by the software installer.

----------------

ChemBytesDraw.dll is a donation of ChemBytes, Switzerland (https://chembytes.com)

---------------

Libinchi is open source from InChI Trust: https://www.inchi-trust.org/downloads/

The libinchi folder content is copied during each build to the main output directory 
with following compile pre-build event:

XCOPY "$(SolutionDir)References\libinchi\" "$(TargetDir)\libinchi" /S /Y /I