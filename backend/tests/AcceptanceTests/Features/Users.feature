Feature: List users

  Scenario: Listing returns every user sorted by last name then first name
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
      | Albert     | Leblanc   |
    When I list the users
    Then the response status is 200
    And the users listed are
      | Name           |
      | Marc Assin     |
      | Albert Leblanc |
      | Juste Leblanc  |

  Scenario: Listing returns no user when none exists
    When I list the users
    Then the response status is 200
    And no user is listed
