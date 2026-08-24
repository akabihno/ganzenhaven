using Godot;
using System;

public partial class Node2d : Sprite2D
{
	private int _speed = 10;
	private float _angularSpeed = Mathf.Pi;
	
	public override void _Ready()
	{
	}

	public override void _Process(double delta)
	{
		Rotation += _angularSpeed * (float)delta;
	}
}
