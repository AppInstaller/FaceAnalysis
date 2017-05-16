 # FaceAnalysis
FaceAnalysis app with optional packages that showcase content and code optional packages. 

The base app FaceAnalysis can find a face/ faces in a photo. To see this app in action, just deploy FaceAnalysis app and open a photo via the file picker.

Next deploy the FilterOptionalPackage. You will now see that the FaceAnalysis app has a new UI elment called "Filters". The FilterOptionalPackage project is a (content only) optional package that has filter images like glasses and moustache under the contents folder. It allows users to make their photos fun by adding filters. 

Next deploy the AgeAnalysisOptionalPackage. Now you will see a new UI element "Utilities". This optional package is an optional package that contains a dll that returns a random number for age (you get the idea..). Clicking on calculate age, loads the dll from the optional package. Since code is involved, AgeAnalsisOptionalPackage and FaceAnalysis are in a related set. In Visual Studio 2017, this is done by creating a bundle.mapping.txt.

For a full video explaining optional packages with this example app check out - https://channel9.msdn.com/events/Build/2017/B8093 (Jump to 22:20)
