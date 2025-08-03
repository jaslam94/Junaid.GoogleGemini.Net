#!/bin/bash

echo "========================================"
echo "Junaid.GoogleGemini.Net Example Console"
echo "========================================"
echo ""

# Check if API key is set as environment variable
if [ -z "$GeminiApiKey" ]; then
    echo "WARNING: GeminiApiKey environment variable not set!"
    echo ""
    echo "Please set your API key first:"
    echo "  export GeminiApiKey=your-actual-api-key-here"
    echo ""
    echo "Or update appsettings.json with your API key."
    echo ""
    read -p "Press any key to continue..."
    exit 1
fi

echo "API Key found in environment variable."
echo "Starting example application..."
echo ""

dotnet run

echo ""
echo "Application completed."
read -p "Press any key to continue..."