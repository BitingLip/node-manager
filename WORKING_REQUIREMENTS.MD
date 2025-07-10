Beyond installing the CPU‐only PyTorch wheel, you’ll need just a handful of pieces to glue everything together:

---

### 1. AMD DirectML-Capable Driver & OS

* **Windows 11** (any up-to-date 22H2 build or later; WDDM 2.7+).
* **AMD Adrenalin 23.40.27.06** (or newer) installed as a *clean* install.

  * Download from AMD Support → Radeon 6000 Series → RX 6800 → “Adrenalin 23.40.27.06” → choose **Clean Install** → reboot.

---

### 2. Isolated Python Environment

* **Python 3.10** (Conda is easiest—`conda create -n dml_env python=3.10 && conda activate dml_env`).
* Update `pip`, `setuptools`, `wheel` early:

  ```powershell
  python -m pip install --upgrade pip setuptools wheel
  ```

---

### 3. Install the Key Packages

```powershell
# 1) PyTorch CPU‐only
pip install torch==2.4.1+cpu \
    --extra-index-url https://download.pytorch.org/whl/cpu

# 2) DirectML backend
pip install torch-directml==0.2.5.dev240914 \
    -f https://download.microsoft.com/whl/torch-directml

# 3) Diffusers & friends
pip install diffusers==0.33.1 transformers safetensors \
            open-clip-torch==2.32.0 torchvision==0.19.1+cpu \
            torchmetrics==1.7.4 onnxruntime-directml==1.22.0
```

> **Tip:** Always use the `+cpu` builds for any `torch*` wheels so no CUDA runtimes sneak in.

---

### 4. Create & Import the Runtime Patch

**File:** `dml_patch.py` (place in your project root):

```python
import torch, torch_directml as dml

# Capture your DML device
DEVICE = dml.device(0)

# Force off all CUDA checks
torch.cuda.is_available = lambda: False
torch.backends.cuda.is_built = False

# Monkey-patch Tensor.to / .cuda
_orig_to = torch.Tensor.to
def _to(self, *args, **kw):
    if args and (args[0]=="cuda" or (hasattr(args[0],"type") and args[0].type=="cuda")):
        return _orig_to(self, DEVICE, *args[1:], **kw)
    return _orig_to(self, *args, **kw)
torch.Tensor.to   = _to
torch.Tensor.cuda = lambda self, *a, **k: self.to(DEVICE)

# Monkey-patch Module.to / .cuda
_orig_m_to = torch.nn.Module.to
def _m_to(self, *args, **kw):
    if args and (args[0]=="cuda" or (hasattr(args[0],"type") and args[0].type=="cuda")):
        return _orig_m_to(self, DEVICE, *args[1:], **kw)
    return _orig_m_to(self, *args, **kw)
torch.nn.Module.to   = _m_to
torch.nn.Module.cuda = lambda self, *a, **k: self.to(DEVICE)
```

**In every script** that loads/uses models, do *as the very first line*:

```python
import dml_patch
```

---

### 5. Clean Up Any CUDA DLLs from Your PATH

In your PowerShell session (before running Python), strip CUDA folders out of `PATH`:

```powershell
$old = [Environment]::GetEnvironmentVariable("PATH","Process")
$clean = ($old.Split(";") | Where-Object { $_ -notmatch "CUDA" })
[Environment]::SetEnvironmentVariable("PATH", ($clean -join ";"), "Process")
```

---

### 6. (Optional) Environment Variables to Harden

If you want to be extra-safe against stray CUDA calls:

```powershell
$Env:CUDA_VISIBLE_DEVICES = ""
$Env:TORCH_CUDA_ARCH_LIST   = ""
```

---

### 7. Verify Everything

Run a quick smoke test—save this as `test_dml.py`:

```python
import dml_patch, torch, torch_directml as dml

print("CUDA?", torch.cuda.is_available())       # → False
print("DML devices:", dml.device_count())       # → 1+
print("Device desc:", dml.device(0).description)

# Move a tensor “to cuda” (really to DML)
x = torch.randn(2,2)
y = x.cuda()
print("y.device:", y.device)                    # → DML:0
```

```powershell
python test_dml.py
```

If that all succeeds, you’re ready to load SDXL (or any other PyTorch model) without touching its source code.
