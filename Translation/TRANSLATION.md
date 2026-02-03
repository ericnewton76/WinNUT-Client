# Translation
WinNUT-Client V2 is natively multilingual, so it is no longer necessary to select your language from the software interface.
Currently, WinNUT-Client supports:
- English
- German
- French
- Simplified Chinese
- Russian

#### To add / correct a language

##### Method 1 (preferred)
1. Fork this repository
2. In the translation directory:

	For a new translation:
	1. Use the new_translation.csv file to translate the texts
	2. Save this file in xx-XX corresponding to the language code

	For a correction:
	1. Edit the wrong language file
	2. Make the necessary corrections

3. Save it instead
4. Create a pull request on this repository to take into account the translation.

##### Method 2
  1. Get the file [new_translation.csv](./Translation/new_translation.csv)
  2. Perform the necessary translations
  3. Save this file in csv format (IMPORTANT)
  4. Create a gist via [gist github](https://gist.github.com) and paste the contents of the previously created csv file
  5. Open a new issue and tell me:
	- the link of the gist
	- the language to create / correct

Your translation / correction will be added on a new version and will thus be available to the entire community.
