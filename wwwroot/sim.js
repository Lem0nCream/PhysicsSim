window.sim = {
    canvas: null,
    ctx: null,
    dotNetHelper: null,
    canvasClickHandler: null,
    start: function () {
        this.canvas = document.getElementById("simCanvas");
        this.ctx = this.canvas.getContext("2d");
    },
    registerCanvasClickHandler: function (dotNetHelper) {
        if (!this.ctx || !this.canvas) {
            this.start();
        }

        if (this.canvasClickHandler) {
            this.canvas.removeEventListener("pointerdown", this.canvasClickHandler);
        }

        this.dotNetHelper = dotNetHelper;
        this.canvasClickHandler = (event) => {
            const rect = this.canvas.getBoundingClientRect();
            const scaleX = this.canvas.width / rect.width;
            const scaleY = this.canvas.height / rect.height;
            const x = (event.clientX - rect.left) * scaleX;
            const y = (event.clientY - rect.top) * scaleY;
            this.dotNetHelper.invokeMethodAsync("SpawnOnClick", x, y);
        };

        this.canvas.addEventListener("pointerdown", this.canvasClickHandler);
    },
    unregisterCanvasClickHandler: function () {
        if (this.canvas && this.canvasClickHandler) {
            this.canvas.removeEventListener("pointerdown", this.canvasClickHandler);
            this.canvasClickHandler = null;
        }

        if (this.dotNetHelper) {
            this.dotNetHelper.dispose();
            this.dotNetHelper = null;
        }
    },
    buildCube: function(x, y, width, height, color) {
        if (!this.ctx || !this.canvas) {
            this.start();
        }

        this.ctx.fillStyle = color;
        this.ctx.fillRect(x, y, width, height);
    },
    clear: function() {
        if (!this.ctx || !this.canvas) {
            this.start();
        }

        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
    },
    renderFrame: function (objects) {
        if (!this.ctx || !this.canvas) {
            this.start();
        }

        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);

        for (let i = 0; i < objects.length; i++) {
            const o = objects[i];
            this.buildCube(o.x, o.y, o.l, o.h, o.c);
        }
    }
};