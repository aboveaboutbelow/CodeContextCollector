# Regenerating Icons for CodeContextCollector

## Original Images
- document.png (512x512, white icon on transparent background)
- note.png (512x512, white icon on transparent background)

## FFmpeg Command
```
ffmpeg -i "document.png" -i "note.png" -filter_complex "[0]scale=16:16,negate=negate_alpha=0[a]; [1]scale=16:16,negate=negate_alpha=0[b]; [0]scale=16:16[c]; [1]scale=16:16[d]; [a][b][c][d]hstack=inputs=4" "CombinedIcons_16x16x4_DarkLight.png"
```

## Result
CombinedIcons_16x16x4_DarkLight.png (64x16)
Contains 4 icons (left to right):
1. Inverted document (dark theme)
2. Inverted note (dark theme)
3. Original document (light theme)
4. Original note (light theme)

## VSCT File
Update the VSCT file to use the new combined image:
- Set href to "Resources\CombinedIcons_16x16x4_DarkLight.png"
- Use IDs 1-4 for dark/light versions of each icon
