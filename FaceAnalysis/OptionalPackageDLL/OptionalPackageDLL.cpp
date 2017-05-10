#include "pch.h"
#include "OptionalPackageDLL.h"
#include "StdLib.h"

// Optional Package Code
// **NOTE** THIS IS A FAKE AGE GENERATOR
__declspec(dllexport) int __cdecl GetAge()
{	
	int lowestAge = 1;
	int highestAge = 85;

	return rand() % ((lowestAge - highestAge) + 1) + lowestAge;
}

