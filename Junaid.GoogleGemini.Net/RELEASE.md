﻿# Release Notes

## v2.1.0
Added stream generate content method to all services.

## v2.0.0
- Read API key from Environment variables. 
- Refactored services and models. 
- Added Text method to just read the string "text" from the API response.

## v1.0.3
- Added configuration paramater to content generation method. It will be used to configure model and apply safety setting while generating content. 
- Removed the unused Newtonsoft.Json package from the project.

## v1.0.2
Added chat service to use Gemini Content API using text input to build freeform conversations across multiple turns.

## v1.0.1
Added vision service to use Gemini Content API using Text-and-image input.

## v1.0.0
Added a service to use Gemini Content API using Text-only input.