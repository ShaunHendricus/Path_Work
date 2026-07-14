#!/usr/bin/env python
import rclpy
from sample_infield_correction import Sample, InfieldCorrectionOperation, declare_and_get_parameter_enum
from capture_assistant import Assistant

def main(args=None):
    rclpy.init(args=args)

    try:
        # --- Capture Assistant ---
        assistant_node = Assistant()

        # 1. Suggest settings
        future = assistant_node.capture_assistant_suggest_settings()
        rclpy.spin_until_future_complete(assistant_node, future, timeout_sec=30)
        result = future.result()
        if not result:
            raise RuntimeError("Failed to get suggested settings from capture assistant")
        yaml_string = result.suggested_settings

        # 2. Save to file
        settings_file = "/home/vedran/ros2_ws/capture_settings.yaml"
        assistant_node.save_settings_to_file(yaml_string, settings_file)

        # 3. Apply settings
        if hasattr(assistant_node, "apply_settings"):
            assistant_node.apply_settings(settings_file)

        # --- Infield Correction ---
        sample_node = Sample()
        operation = InfieldCorrectionOperation.CorrectAndWrite
        sample_node.run_operation(operation)

        rclpy.spin(sample_node)

    except KeyboardInterrupt:
        pass
    finally:
        rclpy.shutdown()


if __name__ == "__main__":
    main()