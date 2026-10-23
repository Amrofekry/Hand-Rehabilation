"""Small trainable temporal models; fixed shape ONNX deployment."""
import torch
from torch import nn
class Temporal(nn.Module):
 def __init__(self,kind="gru",hidden=32):
  super().__init__();self.kind=kind
  if kind in {"gru","lstm"}:self.temporal=(nn.GRU if kind=="gru" else nn.LSTM)(8,hidden,batch_first=True)
  elif kind=="cnn":self.temporal=nn.Sequential(nn.Conv1d(8,hidden,3,padding=1),nn.ReLU(),nn.Conv1d(hidden,hidden,3,padding=1),nn.ReLU())
  else:raise ValueError(kind)
  self.head=nn.Linear(hidden,1)
 def forward(self,x):
  if self.kind=="cnn":y=self.temporal(x.transpose(1,2)).mean(dim=2)
  else:y=self.temporal(x)[0][:,-1,:]
  return self.head(y).squeeze(-1)
