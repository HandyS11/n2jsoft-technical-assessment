Feature: Activate and deactivate a user

  Scenario: Deactivating an active user
    Given the following users exist
      | First name | Last name | Status |
      | Juste      | Leblanc   | Active |
    When I deactivate the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "Inactive"

  Scenario: Activating an inactive user
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Inactive |
    When I activate the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "Active"

  Scenario Outline: Asking for the status a user already has changes nothing
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | <status> |
    When I <action> the user "Juste Leblanc"
    Then the response status is 204
    When I get the user "Juste Leblanc"
    Then the user returned has the status "<status>"

    Examples:
      | action     | status   |
      | activate   | Active   |
      | deactivate | Inactive |

  Scenario Outline: Changing the status of an unknown user answers not found
    When I <action> an unknown user
    Then the response is a problem with status 404

    Examples:
      | action     |
      | activate   |
      | deactivate |

  Scenario: A user saved by another request since it was read is a conflict, whichever store saves it
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When two requests deactivate the user "Juste Leblanc" at the same time, the second saving through the expense report store
    Then the second request is refused as a conflict
