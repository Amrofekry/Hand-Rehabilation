PYTHON ?= .venv-ai/Scripts/python.exe
export PYTHONPATH := src
.PHONY: all acquire train test
all: acquire train test
acquire:
	$(PYTHON) -c "from fatigue_ai.data import acquire;acquire('.')"
train:
	$(PYTHON) -m fatigue_ai.train --root .
test:
	$(PYTHON) -m pytest tests -q --basetemp ../Logs/ai-test-make
