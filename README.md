# Games Against Misinformation - Deepfakes

This is a Unity project designed to educate players on identifying misinformation and deepfakes through an interactive gameplay experience.

## Project Setup

### 1. Cloning the Repository

Clone the project to your local machine using Git:

```bash
git clone https://github.com/Yegmina/GamesAgainstMisinformation-Deepfakes.git
```

### 2. Opening in Unity

- The project is built with **Unity 6 (6000.3.16f1)**.
- Open Unity Hub, click "Add", and select the project folder.
- Ensure you are using the correct Unity version to avoid compatibility issues.

## Running the Game

### Starting Scene

To ensure all systems initialize correctly, the game must be started from the **StartGame** scene:

1. In the `Project` window, navigate to `Assets/Scenes`.
2. Double-click `StartGame.unity` to open it.
3. Press the **Play** button in the Unity Editor.

### Backend Dependency

The game relies on an external API to load dynamic content (news, emails, etc.).

- By default, the game connects to a local backend at `http://127.0.0.1:8765`.
- If the backend is not running, dynamic gameplay elements may fail to load.

## Gameplay Features

- **Interactive Environment:** Explore the apartment and interact with various objects.
- **Computer and Phone:** Use in-game devices to check news, read emails, and browse social media.
- **Paranoia System:** Your choices and trust in sources affect your "paranoia" level, which determines the game's ending.
