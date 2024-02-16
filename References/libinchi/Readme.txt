This folder content is copied during each build to the main output directory 
with following compile pre-build event:

XCOPY "$(SolutionDir)References\libinchi\" "$(TargetDir)\libinchi" /S /Y /I