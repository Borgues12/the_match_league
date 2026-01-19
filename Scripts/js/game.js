// ========================================
// MATCH LEAGUE - Grid Matching Game
// VERSION MEJORADA - Tablero Horizontal
// ========================================

// ========================================
// CONFIGURACION DE DIFICULTAD
// Tablero MAS ANCHO que ALTO (cols > rows)
// ========================================
const DIFFICULTY = {
    easy: {
        cols: 12,         // Columnas (ancho)
        rows: 6,          // Filas (alto)
        cellSize: 60,     // Tamano de celda
        imageCount: 28    // Imagenes a colocar
    },
    medium: {
        cols: 16,
        rows: 8,
        cellSize: 50,
        imageCount: 56
    },
    hard: {
        cols: 20,
        rows: 10,
        cellSize: 40,
        imageCount: 90
    }
};

// ========================================
// CLASE PRINCIPAL DEL JUEGO
// ========================================
class MatchLeagueGame {
    constructor() {
        // Estado del juego
        this.currentDifficulty = 'easy';
        this.cols = DIFFICULTY.easy.cols;
        this.rows = DIFFICULTY.easy.rows;
        this.cellSize = DIFFICULTY.easy.cellSize;

        // Datos del tablero
        this.grid = [];
        this.images = [];
        this.player = { x: 0, y: 0 };
        this.selectedImages = [];

        // Estadisticas
        this.score = 0;
        this.moves = 0;
        this.timeElapsed = 0;
        this.totalImages = 0;
        this.imagesRemaining = 0;
        this.comboMultiplier = 1;
        this.lastMatchTime = 0;

        // Estado
        this.timerInterval = null;
        this.gameStarted = false;
        this.isPaused = false;
        this.currentLoteId = null;

        // Emojis de respaldo
        this.FALLBACK_EMOJIS = [
            { id: 1, emoji: '🎮', name: 'Game' },
            { id: 2, emoji: '👾', name: 'Alien' },
            { id: 3, emoji: '🕹️', name: 'Joystick' },
            { id: 4, emoji: '⭐', name: 'Star' },
            { id: 5, emoji: '💎', name: 'Diamond' },
            { id: 6, emoji: '🎯', name: 'Target' },
            { id: 7, emoji: '🔥', name: 'Fire' },
            { id: 8, emoji: '⚡', name: 'Lightning' }
        ];

        // Cache de elementos DOM
        this.domCache = {};

        // Inicializar
        this.init();
    }

    // ========================================
    // INICIALIZACION
    // ========================================
    init() {
        this.cacheDOM();
        this.setupEventListeners();
        this.loadLoteImages();
        this.updateDisplay();
        this.updateDifficultyDisplay();
    }

    cacheDOM() {
        // Cachear elementos DOM para mejor rendimiento
        this.domCache = {
            gameBoard: document.getElementById('gameBoard'),
            btnStart: document.getElementById('btnStart'),
            btnPause: document.getElementById('btnPause'),
            btnRestart: document.getElementById('btnRestart'),
            btnPlayAgain: document.getElementById('btnPlayAgain'),
            btnMatch: document.getElementById('btnMatch'),
            pauseOverlay: document.getElementById('pauseOverlay'),
            modalGameOver: document.getElementById('modalGameOver'),
            instructions: document.getElementById('instructions'),
            score: document.getElementById('score'),
            timer: document.getElementById('timer'),
            remaining: document.getElementById('remaining'),
            selectionCount: document.getElementById('selectionCount'),
            finalScore: document.getElementById('finalScore'),
            finalTime: document.getElementById('finalTime'),
            finalCleared: document.getElementById('finalCleared'),
            finalRemaining: document.getElementById('finalRemaining')
        };
    }

    setupEventListeners() {
        // Botones principales
        this.domCache.btnStart?.addEventListener('click', () => this.startGame());
        this.domCache.btnPause?.addEventListener('click', () => this.togglePause());
        this.domCache.btnRestart?.addEventListener('click', () => this.restartGame());
        this.domCache.btnPlayAgain?.addEventListener('click', () => this.restartGame());
        this.domCache.btnMatch?.addEventListener('click', () => this.performMatch());

        // Selector de dificultad
        document.querySelectorAll('.difficulty-btn').forEach(btn => {
            btn.addEventListener('click', (e) => this.handleDifficultyChange(e));
        });

        // Controles de teclado (usar bind para mantener contexto)
        this.boundKeyHandler = this.handleKeyPress.bind(this);
        document.addEventListener('keydown', this.boundKeyHandler);
    }

    handleDifficultyChange(e) {
        if (this.gameStarted) return;

        const btn = e.currentTarget;
        document.querySelectorAll('.difficulty-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');

        this.currentDifficulty = btn.dataset.difficulty;
        this.updateDifficultySettings();
        this.updateDifficultyDisplay();
    }

    updateDifficultySettings() {
        const settings = DIFFICULTY[this.currentDifficulty];
        this.cols = settings.cols;
        this.rows = settings.rows;
        this.cellSize = settings.cellSize;
    }

    updateDifficultyDisplay() {
        const labels = {
            easy: `EASY (${DIFFICULTY.easy.cols}x${DIFFICULTY.easy.rows})`,
            medium: `MEDIUM (${DIFFICULTY.medium.cols}x${DIFFICULTY.medium.rows})`,
            hard: `HARD (${DIFFICULTY.hard.cols}x${DIFFICULTY.hard.rows})`
        };

        document.querySelectorAll('.difficulty-btn').forEach(btn => {
            const diff = btn.dataset.difficulty;
            if (labels[diff]) {
                btn.textContent = labels[diff];
            }
        });
    }

    // ========================================
    // CARGAR IMAGENES
    // ========================================
    async loadLoteImages() {
        if (this.domCache.btnStart) {
            this.domCache.btnStart.disabled = true;
        }

        try {
            const response = await fetch('/Game/GetDefaultLoteImages');
            if (!response.ok) throw new Error('Error de red');

            const data = await response.json();

            if (data.success && data.images?.length > 0) {
                this.images = data.images.map(img => ({
                    id: img.id,
                    url: img.url,
                    name: img.nombre || `Imagen ${img.orden}`
                }));
                this.currentLoteId = data.loteId;
            } else {
                console.warn('No hay lote configurado, usando emojis de respaldo');
                this.useFallbackImages();
            }
        } catch (error) {
            console.error('Error cargando imagenes:', error);
            this.useFallbackImages();
        } finally {
            if (this.domCache.btnStart) {
                this.domCache.btnStart.disabled = false;
            }
        }
    }

    useFallbackImages() {
        this.images = this.FALLBACK_EMOJIS.map(e => ({
            id: e.id,
            emoji: e.emoji,
            name: e.name,
            url: null
        }));
    }

    // ========================================
    // CREAR TABLERO (HORIZONTAL)
    // ========================================
    createBoard() {
        const gameBoard = this.domCache.gameBoard;
        if (!gameBoard) return;

        const settings = DIFFICULTY[this.currentDifficulty];

        // Ocultar instrucciones
        if (this.domCache.instructions) {
            this.domCache.instructions.style.display = 'none';
        }

        // Configurar grid CSS - MAS ANCHO QUE ALTO
        const totalWidth = this.cols * this.cellSize + (this.cols - 1) * 2;
        const totalHeight = this.rows * this.cellSize + (this.rows - 1) * 2;

        Object.assign(gameBoard.style, {
            display: 'grid',
            gridTemplateColumns: `repeat(${this.cols}, ${this.cellSize}px)`,
            gridTemplateRows: `repeat(${this.rows}, ${this.cellSize}px)`,
            gap: '2px',
            width: `${totalWidth}px`,
            height: `${totalHeight}px`,
            margin: '0',  // Alineado a la izquierda
            padding: '10px'
        });

        gameBoard.innerHTML = '';

        // Inicializar grid vacio
        this.grid = Array.from({ length: this.rows }, () =>
            Array.from({ length: this.cols }, () => ({
                type: 'empty',
                imageId: null,
                imageData: null,
                element: null
            }))
        );

        // Crear celdas usando DocumentFragment para mejor rendimiento
        const fragment = document.createDocumentFragment();

        for (let y = 0; y < this.rows; y++) {
            for (let x = 0; x < this.cols; x++) {
                const cell = document.createElement('div');
                cell.className = 'grid-cell';
                cell.dataset.x = x;
                cell.dataset.y = y;
                cell.style.width = `${this.cellSize}px`;
                cell.style.height = `${this.cellSize}px`;

                // Click para mover el jugador
                cell.addEventListener('click', () => this.handleCellClick(x, y));

                fragment.appendChild(cell);
                this.grid[y][x].element = cell;
            }
        }

        gameBoard.appendChild(fragment);

        // Colocar imagenes y jugador
        this.placeRandomImages(settings.imageCount);
        this.placePlayer();

        // Actualizar contadores
        this.imagesRemaining = this.totalImages;
        this.updateDisplay();
    }

    handleCellClick(x, y) {
        if (!this.gameStarted || this.isPaused) return;

        // Mover al jugador a esa celda
        this.player.x = x;
        this.player.y = y;
        this.moves++;
        this.updatePlayerPosition();
    }

    placeRandomImages(count) {
        if (this.images.length === 0) {
            console.error('No hay imagenes para colocar');
            return;
        }

        // Crear pool de imagenes balanceado
        const minPerType = 4;
        let imagePool = [];

        this.images.forEach(img => {
            const appearances = Math.max(minPerType, Math.ceil(count / this.images.length));
            for (let i = 0; i < appearances; i++) {
                imagePool.push({ ...img });
            }
        });

        // Mezclar y limitar
        this.shuffleArray(imagePool);
        imagePool = imagePool.slice(0, Math.min(count, this.cols * this.rows - 1));
        this.totalImages = imagePool.length;

        // Generar posiciones aleatorias unicas
        const positions = [];
        for (let y = 0; y < this.rows; y++) {
            for (let x = 0; x < this.cols; x++) {
                positions.push({ x, y });
            }
        }
        this.shuffleArray(positions);

        // Colocar imagenes
        for (let i = 0; i < imagePool.length && i < positions.length; i++) {
            const pos = positions[i];
            const img = imagePool[i];
            const cell = this.grid[pos.y][pos.x];

            cell.type = 'image';
            cell.imageId = img.id;
            cell.imageData = img;

            this.renderCellImage(cell, img);
        }
    }

    renderCellImage(cell, img) {
        const el = cell.element;
        el.classList.add('has-image');

        if (img.url) {
            el.style.backgroundImage = `url('${img.url}')`;
            el.style.backgroundSize = 'cover';
            el.style.backgroundPosition = 'center';
        } else if (img.emoji) {
            const emojiSize = Math.max(16, this.cellSize * 0.5);
            el.innerHTML = `<span class="cell-emoji" style="font-size:${emojiSize}px">${img.emoji}</span>`;
        }
    }

    placePlayer() {
        // Buscar celda vacia desde el centro
        const centerX = Math.floor(this.cols / 2);
        const centerY = Math.floor(this.rows / 2);

        // Busqueda en espiral desde el centro
        for (let radius = 0; radius < Math.max(this.cols, this.rows); radius++) {
            for (let dx = -radius; dx <= radius; dx++) {
                for (let dy = -radius; dy <= radius; dy++) {
                    const x = centerX + dx;
                    const y = centerY + dy;

                    if (this.isValidPosition(x, y) && this.grid[y][x].type === 'empty') {
                        this.player.x = x;
                        this.player.y = y;
                        this.updatePlayerPosition();
                        return;
                    }
                }
            }
        }

        // Fallback: forzar posicion
        this.player.x = 0;
        this.player.y = 0;
        this.clearCell(0, 0);
        this.updatePlayerPosition();
    }

    isValidPosition(x, y) {
        return x >= 0 && x < this.cols && y >= 0 && y < this.rows;
    }

    clearCell(x, y) {
        const cell = this.grid[y][x];
        cell.type = 'empty';
        cell.imageId = null;
        cell.imageData = null;
        cell.element.classList.remove('has-image', 'selected', 'matched');
        cell.element.style.backgroundImage = '';
        cell.element.innerHTML = '';
    }

    updatePlayerPosition() {
        // Remover clase de todas las celdas
        document.querySelectorAll('.grid-cell.player').forEach(cell => {
            cell.classList.remove('player');
        });

        // Agregar al jugador
        const cell = this.grid[this.player.y]?.[this.player.x];
        if (cell?.element) {
            cell.element.classList.add('player');
        }
    }

    // ========================================
    // CONTROLES DEL JUEGO
    // ========================================
    handleKeyPress(e) {
        // Evitar si hay input activo
        const activeTag = document.activeElement?.tagName?.toLowerCase();
        if (activeTag === 'input' || activeTag === 'textarea') return;

        // Tecla H para pausar (siempre disponible si el juego empezo)
        if (e.key.toLowerCase() === 'h' && this.gameStarted) {
            e.preventDefault();
            this.togglePause();
            return;
        }

        if (!this.gameStarted || this.isPaused) return;

        const actions = {
            'arrowup': () => this.movePlayer(0, -1),
            'w': () => this.movePlayer(0, -1),
            'arrowdown': () => this.movePlayer(0, 1),
            's': () => this.movePlayer(0, 1),
            'arrowleft': () => this.movePlayer(-1, 0),
            'a': () => this.movePlayer(-1, 0),
            'arrowright': () => this.movePlayer(1, 0),
            'd': () => this.movePlayer(1, 0),
            ' ': () => this.selectImage(),
            'enter': () => this.selectedImages.length >= 4 && this.performMatch(),
            'escape': () => this.cancelSelection()
        };

        const action = actions[e.key.toLowerCase()];
        if (action) {
            e.preventDefault();
            action();
        }
    }

    movePlayer(dx, dy) {
        const newX = this.player.x + dx;
        const newY = this.player.y + dy;

        if (!this.isValidPosition(newX, newY)) return;

        this.player.x = newX;
        this.player.y = newY;
        this.moves++;
        this.updatePlayerPosition();
    }

    // ========================================
    // SISTEMA DE SELECCION
    // ========================================
    selectImage() {
        const cell = this.grid[this.player.y][this.player.x];

        if (cell.type !== 'image') return;

        // Verificar si ya esta seleccionada
        const existingIndex = this.selectedImages.findIndex(
            img => img.x === this.player.x && img.y === this.player.y
        );

        if (existingIndex > -1) {
            // Deseleccionar
            this.deselectImage(this.player.x, this.player.y);
            return;
        }

        // Verificar tipo diferente
        if (this.selectedImages.length > 0 && this.selectedImages[0].imageId !== cell.imageId) {
            this.cancelSelection();
        }

        // Seleccionar
        this.selectedImages.push({
            x: this.player.x,
            y: this.player.y,
            imageId: cell.imageId
        });

        cell.element.classList.add('selected');

        // Mostrar boton de match si hay 4+
        if (this.selectedImages.length >= 4) {
            this.showMatchButton();
        }

        this.updateSelectionDisplay();
    }

    deselectImage(x, y) {
        const index = this.selectedImages.findIndex(img => img.x === x && img.y === y);
        if (index === -1) return;

        this.selectedImages.splice(index, 1);
        this.grid[y]?.[x]?.element?.classList.remove('selected');

        if (this.selectedImages.length < 4) {
            this.hideMatchButton();
        }

        this.updateSelectionDisplay();
    }

    cancelSelection() {
        this.selectedImages.forEach(img => {
            this.grid[img.y]?.[img.x]?.element?.classList.remove('selected');
        });
        this.selectedImages = [];
        this.hideMatchButton();
        this.updateSelectionDisplay();
    }

    showMatchButton() {
        this.domCache.btnMatch?.classList.add('visible');
    }

    hideMatchButton() {
        this.domCache.btnMatch?.classList.remove('visible');
    }

    updateSelectionDisplay() {
        if (this.domCache.selectionCount) {
            this.domCache.selectionCount.textContent = this.selectedImages.length;
        }
    }

    // ========================================
    // SISTEMA DE MATCH
    // ========================================
    performMatch() {
        if (this.selectedImages.length < 4) return;

        const count = this.selectedImages.length;

        // Sistema de puntuacion mejorado
        const basePoints = this.calculatePoints(count);

        // Sistema de combo (matches rapidos)
        const now = Date.now();
        if (now - this.lastMatchTime < 5000) {
            this.comboMultiplier = Math.min(this.comboMultiplier + 0.2, 3);
        } else {
            this.comboMultiplier = 1;
        }
        this.lastMatchTime = now;

        const finalPoints = Math.floor(basePoints * this.comboMultiplier);

        // Animacion de eliminacion
        this.selectedImages.forEach((img, index) => {
            const cell = this.grid[img.y][img.x];

            // Retraso escalonado para efecto visual
            setTimeout(() => {
                cell.element.classList.add('matched');

                setTimeout(() => {
                    this.clearCell(img.x, img.y);
                }, 400);
            }, index * 50);
        });

        // Actualizar estadisticas
        this.score += finalPoints;
        this.imagesRemaining -= count;

        // Mostrar puntos flotantes
        this.showFloatingPoints(finalPoints, this.comboMultiplier > 1);

        // Limpiar seleccion
        this.selectedImages = [];
        this.hideMatchButton();
        this.updateDisplay();

        // Verificar fin del juego
        setTimeout(() => this.checkGameEnd(), 600);
    }

    calculatePoints(count) {
        // Sistema de puntos progresivo
        const pointsTable = {
            4: 100,
            5: 200,
            6: 350,
            7: 500
        };

        if (count <= 7) {
            return pointsTable[count] || 100;
        }

        // Bonus por mas de 7
        return 500 + (count - 7) * 200;
    }

    showFloatingPoints(points, isCombo = false) {
        const playerCell = this.grid[this.player.y]?.[this.player.x]?.element;
        if (!playerCell) return;

        const rect = playerCell.getBoundingClientRect();

        const floating = document.createElement('div');
        floating.className = 'floating-points';
        floating.textContent = `+${points}${isCombo ? ' COMBO!' : ''}`;
        floating.style.cssText = `
            left: ${rect.left + this.cellSize / 2}px;
            top: ${rect.top}px;
            ${isCombo ? 'color: #d0af73; font-size: 20px;' : ''}
        `;

        document.body.appendChild(floating);

        setTimeout(() => floating.remove(), 1000);
    }

    checkGameEnd() {
        // Contar imagenes por tipo
        const imageCounts = {};

        for (let y = 0; y < this.rows; y++) {
            for (let x = 0; x < this.cols; x++) {
                const cell = this.grid[y][x];
                if (cell.type === 'image') {
                    const id = cell.imageId;
                    imageCounts[id] = (imageCounts[id] || 0) + 1;
                }
            }
        }

        const canMatch = Object.values(imageCounts).some(count => count >= 4);
        const totalRemaining = Object.values(imageCounts).reduce((a, b) => a + b, 0);

        if (!canMatch || totalRemaining === 0) {
            this.gameOver(totalRemaining === 0);
        }
    }

    // ========================================
    // CONTROL DEL JUEGO
    // ========================================
    startGame() {
        if (this.gameStarted) return;

        this.gameStarted = true;

        // Deshabilitar controles de inicio
        if (this.domCache.btnStart) this.domCache.btnStart.disabled = true;
        document.querySelectorAll('.difficulty-btn').forEach(btn => btn.disabled = true);

        // Aplicar configuracion y crear tablero
        this.updateDifficultySettings();
        this.createBoard();

        // Iniciar timer
        this.timerInterval = setInterval(() => this.updateTimer(), 1000);
    }

    togglePause() {
        if (!this.gameStarted) return;

        this.isPaused = !this.isPaused;

        if (this.isPaused) {
            clearInterval(this.timerInterval);
            if (this.domCache.btnPause) this.domCache.btnPause.textContent = 'RESUME';
            this.domCache.pauseOverlay?.classList.add('show');
        } else {
            this.timerInterval = setInterval(() => this.updateTimer(), 1000);
            if (this.domCache.btnPause) this.domCache.btnPause.textContent = 'PAUSE';
            this.domCache.pauseOverlay?.classList.remove('show');
        }
    }

    restartGame() {
        // Limpiar estado
        clearInterval(this.timerInterval);

        this.score = 0;
        this.moves = 0;
        this.timeElapsed = 0;
        this.gameStarted = false;
        this.isPaused = false;
        this.selectedImages = [];
        this.totalImages = 0;
        this.imagesRemaining = 0;
        this.comboMultiplier = 1;
        this.lastMatchTime = 0;

        // Rehabilitar controles
        if (this.domCache.btnStart) this.domCache.btnStart.disabled = false;
        if (this.domCache.btnPause) this.domCache.btnPause.textContent = 'PAUSE';
        this.domCache.modalGameOver?.classList.remove('show');
        this.domCache.pauseOverlay?.classList.remove('show');

        document.querySelectorAll('.difficulty-btn').forEach(btn => btn.disabled = false);

        // Restaurar tablero a estado inicial
        const gameBoard = this.domCache.gameBoard;
        if (gameBoard) {
            gameBoard.innerHTML = '';
            gameBoard.style.display = 'flex';
            gameBoard.style.alignItems = 'center';
            gameBoard.style.justifyContent = 'center';

            if (this.domCache.instructions) {
                gameBoard.appendChild(this.domCache.instructions);
                this.domCache.instructions.style.display = 'block';
            }
        }

        this.hideMatchButton();
        this.updateDisplay();
    }

    gameOver(completed = false) {
        clearInterval(this.timerInterval);
        this.gameStarted = false;

        const imagesCleared = this.totalImages - this.imagesRemaining;

        // Guardar resultado
        this.saveGameResult(completed);

        // Mostrar modal
        if (this.domCache.finalScore) {
            this.domCache.finalScore.textContent = this.score.toString().padStart(4, '0');
        }
        if (this.domCache.finalTime) {
            this.domCache.finalTime.textContent = this.formatTime(this.timeElapsed);
        }
        if (this.domCache.finalCleared) {
            this.domCache.finalCleared.textContent = imagesCleared;
        }
        if (this.domCache.finalRemaining) {
            this.domCache.finalRemaining.textContent = this.imagesRemaining;
        }

        this.domCache.modalGameOver?.classList.add('show');
    }

    async saveGameResult(completed) {
        try {
            const response = await fetch('/Game/SaveResult', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `puntuacion=${this.score}&tiempo=${this.timeElapsed}&movimientos=${this.moves}&completado=${completed}&idLote=${this.currentLoteId || ''}`
            });

            const data = await response.json();
            console.log('Resultado guardado:', data);
        } catch (error) {
            console.error('Error guardando resultado:', error);
        }
    }

    // ========================================
    // UTILIDADES
    // ========================================
    shuffleArray(array) {
        // Fisher-Yates shuffle
        for (let i = array.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [array[i], array[j]] = [array[j], array[i]];
        }
        return array;
    }

    updateTimer() {
        this.timeElapsed++;
        this.updateDisplay();
    }

    formatTime(seconds) {
        const mins = Math.floor(seconds / 60).toString().padStart(2, '0');
        const secs = (seconds % 60).toString().padStart(2, '0');
        return `${mins}:${secs}`;
    }

    updateDisplay() {
        if (this.domCache.score) {
            this.domCache.score.textContent = this.score.toString().padStart(4, '0');
        }
        if (this.domCache.timer) {
            this.domCache.timer.textContent = this.formatTime(this.timeElapsed);
        }
        if (this.domCache.remaining) {
            this.domCache.remaining.textContent = this.imagesRemaining.toString().padStart(3, '0');
        }
    }
}

// ========================================
// INICIALIZAR JUEGO
// ========================================
document.addEventListener('DOMContentLoaded', () => {
    window.game = new MatchLeagueGame();
});

// Exponer funciones globales para botones con onclick en HTML
window.performMatch = () => window.game?.performMatch();
